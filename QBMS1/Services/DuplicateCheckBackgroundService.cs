using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS1.Services
{
    /// <summary>
    /// Picks up uploaded Word / Excel batches, runs the duplicate check for every question through
    /// the Python API, and - only when every question is clear and the quota still fits - saves the
    /// questions and submits them to the Vetter.
    /// </summary>
    public class DuplicateCheckBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IUploadBatchRepository _uploadBatchRepository;
        private readonly DuplicateCheckSettings _settings;
        private readonly ILogger<DuplicateCheckBackgroundService> _logger;

        public DuplicateCheckBackgroundService(
            IServiceScopeFactory scopeFactory,
            IUploadBatchRepository uploadBatchRepository,
            DuplicateCheckSettings settings,
            ILogger<DuplicateCheckBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _uploadBatchRepository = uploadBatchRepository;
            _settings = settings;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _uploadBatchRepository.ResetStuckBatchesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not reset upload batches at start-up.");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(5);

                try
                {
                    // Keep the Python embedding table in step with the question table, so questions
                    // saved earlier (and older ones) can be matched by this and later uploads.
                    await IndexPendingQuestionsSafelyAsync();

                    var batch = await _uploadBatchRepository.ClaimNextPendingBatchAsync();

                    if (batch != null)
                    {
                        bool retryLater = await ProcessBatchAsync(batch, stoppingToken);

                        delay = retryLater
                            ? TimeSpan.FromSeconds(Math.Max(5, _settings.RetryDelaySeconds))
                            : TimeSpan.Zero;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Upload batch worker error.");
                    delay = TimeSpan.FromSeconds(30);
                }

                if (delay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(delay, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task IndexPendingQuestionsSafelyAsync()
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var duplicateCheckService = scope.ServiceProvider.GetRequiredService<IDuplicateCheckService>();
                    int indexed = await duplicateCheckService.IndexPendingQuestionsAsync();
                    if (indexed > 0) _logger.LogInformation("Indexed {Count} question(s) for duplicate check.", indexed);
                }
            }
            catch (Exception ex)
            {
                // Python API down: the next pass tries again; the batch check below reports it if it is still down.
                _logger.LogWarning(ex, "Question indexing skipped.");
            }
        }

        // Returns true when the batch was handed back (Python API unreachable) and should be retried later.
        private async Task<bool> ProcessBatchAsync(UploadBatchViewModel batch, CancellationToken stoppingToken)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var duplicateCheckService = scope.ServiceProvider.GetRequiredService<IDuplicateCheckService>();
                var quotaService = scope.ServiceProvider.GetRequiredService<IQuestionQuotaService>();
                var questionCreationService = scope.ServiceProvider.GetRequiredService<ISetterQuestionCreationService>();
                var workflowService = scope.ServiceProvider.GetRequiredService<IQuestionWorkflowService>();

                /* =========================================
                   1. DUPLICATE CHECK (resumes where it stopped)
                   ========================================= */

                int processed = batch.Items.Count(x => x.CheckStatus != "PENDING");
                int duplicateCount = batch.Items.Count(x => x.CheckStatus == "DUPLICATE");

                // Semantic duplicates between the rows of this same file.
                List<List<DuplicateMatchModel>> inFileMatches = null;

                if (batch.Items.Any(x => x.CheckStatus == "PENDING"))
                {
                    try
                    {
                        inFileMatches = await duplicateCheckService.GetInFileMatchesAsync(
                            batch.Items.Select(x => x.Question.QuestionText).ToList(),
                            batch.Items.Select(x => x.RowNumber).ToList());
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogWarning(ex, "Duplicate check API unavailable for batch {BatchId}.", batch.BatchId);

                        await _uploadBatchRepository.CompleteBatchAsync(
                            batch.BatchId,
                            "PENDING",
                            "The duplicate check service is not reachable. The check will resume automatically.");

                        return true;
                    }
                }

                foreach (var item in batch.Items.Where(x => x.CheckStatus == "PENDING").ToList())
                {
                    stoppingToken.ThrowIfCancellationRequested();

                    List<DuplicateMatchModel> matches;

                    try
                    {
                        matches = await duplicateCheckService.GetTopMatchesAsync(item.Question.QuestionText);
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogWarning(ex, "Duplicate check API unavailable for batch {BatchId}.", batch.BatchId);

                        await _uploadBatchRepository.CompleteBatchAsync(
                            batch.BatchId,
                            "PENDING",
                            "The duplicate check service is not reachable. The check will resume automatically.");

                        return true;
                    }

                    // Existing questions + other rows of this file, best three overall.
                    int itemIndex = batch.Items.IndexOf(item);
                    if (inFileMatches != null && itemIndex >= 0 && itemIndex < inFileMatches.Count)
                    {
                        matches = matches.Concat(inFileMatches[itemIndex])
                                         .OrderByDescending(x => x.SimilarityPercent)
                                         .Take(3)
                                         .ToList();
                    }

                    decimal topScore = matches.Count == 0 ? 0m : matches.Max(x => x.SimilarityPercent);
                    string checkStatus = topScore >= duplicateCheckService.ThresholdPercent ? "DUPLICATE" : "CLEAR";

                    await _uploadBatchRepository.SaveItemResultAsync(item.ItemId, checkStatus, topScore, matches);

                    item.CheckStatus = checkStatus;
                    processed++;
                    if (checkStatus == "DUPLICATE") duplicateCount++;

                    await _uploadBatchRepository.UpdateProgressAsync(batch.BatchId, processed, duplicateCount);
                }

                if (duplicateCount > 0)
                {
                    await _uploadBatchRepository.CompleteBatchAsync(
                        batch.BatchId,
                        "DUPLICATES_FOUND",
                        duplicateCount + " of " + batch.Items.Count + " question(s) are at or above the "
                            + duplicateCheckService.ThresholdPercent.ToString("0.##")
                            + "% similarity limit. Nothing was saved. Download the result file, replace the flagged questions and upload again.");

                    return false;
                }

                /* =========================================
                   2. QUOTA RE-CHECK (it may have changed since upload)
                   ========================================= */

                var toSave = batch.Items.Where(x => !x.SavedQuestionId.HasValue).ToList();

                var quota = await quotaService.GetQuotaAsync(batch.AssignmentId, batch.SetterUserId);
                var reserved = await _uploadBatchRepository.GetReservedCountsAsync(batch.AssignmentId, batch.SetterUserId, batch.BatchId);

                int easyToSave = toSave.Count(x => string.Equals(x.Question.DifficultyLevel, "EASY", StringComparison.OrdinalIgnoreCase));
                int mediumToSave = toSave.Count(x => string.Equals(x.Question.DifficultyLevel, "MEDIUM", StringComparison.OrdinalIgnoreCase));
                int hardToSave = toSave.Count(x => string.Equals(x.Question.DifficultyLevel, "HARD", StringComparison.OrdinalIgnoreCase));

                if (quota == null
                    || easyToSave > Math.Max(0, quota.Easyremaining - reserved.easy)
                    || mediumToSave > Math.Max(0, quota.Mediumremaining - reserved.medium)
                    || hardToSave > Math.Max(0, quota.Hardremaining - reserved.hard))
                {
                    await _uploadBatchRepository.CompleteBatchAsync(
                        batch.BatchId,
                        "SAVE_FAILED",
                        "The duplicate check is clear, but the remaining question quota no longer covers this file. Nothing was saved.");

                    return false;
                }

                /* =========================================
                   3. SAVE, THEN SUBMIT TO THE VETTER
                   ========================================= */

                await _uploadBatchRepository.CompleteBatchAsync(batch.BatchId, "SAVING", null);

                foreach (var item in toSave)
                {
                    var save = await questionCreationService.SaveQuestionAsync(batch.AssignmentId, batch.SetterUserId, item.Question);

                    if (save == null || !save.Success)
                    {
                        await _uploadBatchRepository.CompleteBatchAsync(
                            batch.BatchId,
                            "SAVE_FAILED",
                            "Row " + item.RowNumber + " could not be saved: " + (save == null ? "No result was returned." : save.ErrorMessage)
                                + " Questions saved before this row remain as DRAFT on the Setter side.");

                        return false;
                    }

                    item.SavedQuestionId = save.QuestionId;
                    await _uploadBatchRepository.SetItemSavedQuestionAsync(item.ItemId, save.QuestionId);
                }

                int notSubmitted = 0;

                foreach (var item in batch.Items.Where(x => x.SavedQuestionId.HasValue))
                {
                    var submitted = await workflowService.SubmitToVetterAsync(item.SavedQuestionId.Value, batch.SetterUserId);
                    if (!submitted) notSubmitted++;
                }

                if (notSubmitted > 0)
                {
                    await _uploadBatchRepository.CompleteBatchAsync(
                        batch.BatchId,
                        "SAVE_FAILED",
                        "Questions were saved, but " + notSubmitted + " could not be submitted to the Vetter. Please check your question list.");

                    return false;
                }

                await _uploadBatchRepository.CompleteBatchAsync(
                    batch.BatchId,
                    "SAVED",
                    batch.Items.Count + " question(s) saved and submitted to the Vetter.");

                return false;
            }
        }
    }
}
