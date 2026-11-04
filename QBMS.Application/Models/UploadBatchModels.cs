using System;
using System.Collections.Generic;

namespace QBMS.Application.Models
{
    public class DuplicateMatchModel
    {
        public long QuestionId { get; set; }

        public string QuestionText { get; set; }

        public decimal SimilarityPercent { get; set; }

        // > 0 when the match is another row of the SAME uploaded file (QuestionId is 0 then).
        public int FileRowNumber { get; set; }
    }

    public class UploadBatchItemModel
    {
        public long ItemId { get; set; }

        public int RowNumber { get; set; }

        public CreateQuestionViewModel Question { get; set; }

        // The 16 cells exactly as uploaded (used to rebuild the result file).
        public string[] RowCells { get; set; }

        // PENDING, CLEAR, DUPLICATE
        public string CheckStatus { get; set; }

        public decimal TopScore { get; set; }

        public List<DuplicateMatchModel> Matches { get; set; }

        public long? SavedQuestionId { get; set; }

        public UploadBatchItemModel()
        {
            Matches = new List<DuplicateMatchModel>();
            CheckStatus = "PENDING";
        }
    }

    public class UploadBatchViewModel
    {
        public long BatchId { get; set; }

        public long AssignmentId { get; set; }

        public long SetterUserId { get; set; }

        public string FileName { get; set; }

        // XLSX or DOCX
        public string FileType { get; set; }

        public string[] HeaderRow { get; set; }

        // PENDING, CHECKING, DUPLICATES_FOUND, SAVING, SAVED, SAVE_FAILED
        public string Status { get; set; }

        public int TotalQuestions { get; set; }

        public int ProcessedQuestions { get; set; }

        public int DuplicateCount { get; set; }

        public string Message { get; set; }

        public DateTime UploadedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public List<UploadBatchItemModel> Items { get; set; }

        // The uploaded Word file itself (only loaded for a single batch); the result file is built from it.
        public byte[] FileData { get; set; }

        public int ProgressPercent
        {
            get
            {
                if (TotalQuestions <= 0)
                {
                    return 0;
                }

                return (int)Math.Min(100, (ProcessedQuestions * 100L) / TotalQuestions);
            }
        }

        // True while the duplicate check / save is still running.
        public bool IsInProgress
        {
            get
            {
                return Status == "PENDING" || Status == "CHECKING" || Status == "SAVING";
            }
        }

        // The result file (all questions + top 3 matches) is available once the check reaches 100%.
        public bool IsReportReady
        {
            get
            {
                return !IsInProgress && ProcessedQuestions >= TotalQuestions && TotalQuestions > 0;
            }
        }

        public UploadBatchViewModel()
        {
            Items = new List<UploadBatchItemModel>();
        }
    }

    public class DuplicateCheckSettings
    {
        // Base address of the offline Python API, e.g. http://localhost:8000/
        public string BaseUrl { get; set; } = "http://localhost:8000/";

        public string CheckPath { get; set; } = "check-duplicate";

        public string CompareBatchPath { get; set; } = "compare-batch";

        public string IndexBatchPath { get; set; } = "index-batch";

        // A question is a duplicate when its best match is at or above this percentage.
        public decimal ThresholdPercent { get; set; } = 85m;

        public int TimeoutSeconds { get; set; } = 60;

        // Wait before the worker retries a batch when the Python API is unreachable.
        public int RetryDelaySeconds { get; set; } = 30;
    }
}
