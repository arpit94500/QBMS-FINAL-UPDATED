using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    /// <summary>
    /// Calls the offline Python similarity API (app.py).
    /// URL, threshold and timeout come from appsettings.json "DuplicateCheck".
    ///
    /// The Python API only knows question ids and embeddings (it cannot decrypt the question table),
    /// so this service sends PLAIN text (and pictures) to it and looks the matched question text up
    /// here, through the repository.
    /// </summary>
    public class DuplicateCheckService : IDuplicateCheckService
    {
        // Questions the Python API could not index (nothing to embed); not retried until restart.
        private static readonly ConcurrentDictionary<long, byte> SkippedIndexIds = new ConcurrentDictionary<long, byte>();

        private readonly HttpClient _httpClient;
        private readonly DuplicateCheckSettings _settings;
        private readonly IUploadBatchRepository _uploadBatchRepository;

        public DuplicateCheckService(HttpClient httpClient, DuplicateCheckSettings settings, IUploadBatchRepository uploadBatchRepository)
        {
            _httpClient = httpClient;
            _settings = settings;
            _uploadBatchRepository = uploadBatchRepository;
        }

        public decimal ThresholdPercent
        {
            get { return _settings.ThresholdPercent; }
        }

        public async Task<List<DuplicateMatchModel>> GetTopMatchesAsync(string questionText)
        {
            var images = new List<string>();
            var text = QuestionContentHelper.Extract(questionText, images);

            // Nothing to compare (empty question, picture-less): no duplicate possible.
            if (text.Length == 0 && images.Count == 0)
            {
                return new List<DuplicateMatchModel>();
            }

            var json = await PostAsync(_settings.CheckPath, new { question = text, images_base64 = images });
            var matches = new List<DuplicateMatchModel>();
            var array = json["matches"] as JArray;

            if (array == null)
            {
                return matches;
            }

            foreach (var token in array)
            {
                matches.Add(new DuplicateMatchModel
                {
                    QuestionId = token.Value<long?>("question_id") ?? 0,
                    SimilarityPercent = ToDecimal(token.Value<double?>("similarity_percent"))
                });
            }

            matches = matches.OrderByDescending(x => x.SimilarityPercent).Take(3).ToList();

            var texts = await _uploadBatchRepository.GetQuestionTextsAsync(matches.Select(x => x.QuestionId).ToList());
            foreach (var match in matches)
            {
                string stored;
                match.QuestionText = texts.TryGetValue(match.QuestionId, out stored)
                    ? QuestionContentHelper.ToPreview(stored, 300)
                    : "";
            }

            return matches;
        }

        public async Task<List<List<DuplicateMatchModel>>> GetInFileMatchesAsync(IList<string> questionTexts, IList<int> rowNumbers)
        {
            var result = questionTexts.Select(x => new List<DuplicateMatchModel>()).ToList();
            if (questionTexts.Count < 2)
            {
                return result;
            }

            var previews = new List<string>();
            var items = new List<object>();
            foreach (var questionText in questionTexts)
            {
                var images = new List<string>();
                var text = QuestionContentHelper.Extract(questionText, images);
                previews.Add(text.Length <= 300 ? text : text.Substring(0, 300) + "...");
                items.Add(new { question = text, images_base64 = images });
            }

            var json = await PostAsync(_settings.CompareBatchPath, new { items = items });
            var rows = json["matches"] as JArray;
            if (rows == null)
            {
                return result;
            }

            for (int i = 0; i < rows.Count && i < result.Count; i++)
            {
                var list = rows[i] as JArray;
                if (list == null) continue;

                foreach (var token in list)
                {
                    int other = token.Value<int?>("index") ?? -1;
                    if (other < 0 || other >= questionTexts.Count || other == i) continue;

                    result[i].Add(new DuplicateMatchModel
                    {
                        QuestionId = 0,
                        FileRowNumber = rowNumbers[other],
                        QuestionText = previews[other],
                        SimilarityPercent = ToDecimal(token.Value<double?>("similarity_percent"))
                    });
                }
            }

            return result;
        }

        public async Task<int> IndexPendingQuestionsAsync()
        {
            int indexed = 0;

            // Up to 4 x 50 questions per call; the worker calls this regularly.
            for (int round = 0; round < 4; round++)
            {
                var pending = await _uploadBatchRepository.GetUnindexedQuestionsAsync(50, SkippedIndexIds.Keys.ToList());
                if (pending.Count == 0)
                {
                    break;
                }

                var items = new List<object>();
                foreach (var question in pending)
                {
                    var images = new List<string>();
                    var text = QuestionContentHelper.Extract(question.Value, images);
                    items.Add(new { question_id = question.Key, question = text, images_base64 = images });
                }

                var json = await PostAsync(_settings.IndexBatchPath, new { items = items });

                var indexedIds = new HashSet<long>();
                var array = json["indexed"] as JArray;
                if (array != null)
                {
                    foreach (var id in array) indexedIds.Add(id.Value<long>());
                }

                foreach (var question in pending)
                {
                    if (!indexedIds.Contains(question.Key))
                    {
                        SkippedIndexIds[question.Key] = 0;
                    }
                }

                indexed += indexedIds.Count;
            }

            return indexed;
        }

        private async Task<JObject> PostAsync(string path, object payload)
        {
            var url = new Uri(new Uri(_settings.BaseUrl), path);

            var body = new StringContent(
                JsonConvert.SerializeObject(payload),
                Encoding.UTF8,
                "application/json");

            using (var response = await _httpClient.PostAsync(url, body))
            {
                response.EnsureSuccessStatusCode();
                return JObject.Parse(await response.Content.ReadAsStringAsync());
            }
        }

        private static decimal ToDecimal(double? value)
        {
            return Convert.ToDecimal(value ?? 0d, CultureInfo.InvariantCulture);
        }
    }
}
