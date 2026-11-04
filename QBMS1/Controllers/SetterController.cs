using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;
using System.Security.Claims;
using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Npgsql;
using QBMS1.Services;
using System.IO.Compression;
using System.Text;

namespace QBMS1.Controllers
{
    [Authorize(Roles = "SETTER")]
    public class SetterController : Controller
    {
        private readonly ISetterService _setterService;
        private readonly IQuestionQuotaService _quotaService;
        private readonly IQuestionWorkflowService _workflowService;
        private readonly ICurrentUserService _currentUser;
        private readonly ISetterQuestionCreationService _questionCreationService;
        private readonly string _qbmsConnectionString;
        private readonly ITosService _tosService;
        private readonly IUploadBatchRepository _uploadBatchRepository;
        private readonly IDuplicateCheckService _duplicateCheckService;
        //    private readonly IQuestionUploadService
        //_questionUploadService;


        //public SetterController(
        //    ISetterService setterService)
        //{
        //    _setterService = setterService;
        //}
        //    public SetterController(
        //ISetterService setterService,
        //IQuestionQuotaService quotaService)
        //    {
        //        _setterService = setterService;
        //        _quotaService = quotaService;
        //    }
        public SetterController(
     ISetterService setterService,
     IQuestionQuotaService quotaService,
     IQuestionWorkflowService workflowService,
     ICurrentUserService currentUser,
     ISetterQuestionCreationService questionCreationService,
     IConfiguration configuration,
     ITosService tosService,
     IUploadBatchRepository uploadBatchRepository,
     IDuplicateCheckService duplicateCheckService)
        {
            _duplicateCheckService = duplicateCheckService;
            _setterService = setterService;
            _quotaService = quotaService;
            _workflowService = workflowService;
            _currentUser = currentUser;
            _questionCreationService = questionCreationService;
            _qbmsConnectionString = configuration.GetConnectionString("QBMS");
            _tosService = tosService;
            _uploadBatchRepository = uploadBatchRepository;
            //    _questionUploadService =
            //questionUploadService;

        }
        //public SetterController(ISetterQuestionCreationService questionCreationService)
        //{
        //    _questionCreationService = questionCreationService;
        //}

        // mode = "excel" or "word" (Upload buttons on the dashboard) shows only that upload form.
        [HttpGet]
        public async Task<IActionResult> UploadQuestions(long assignmentId = 0, string mode = null)
        {
            long setterUserId = long.Parse(
                User.FindFirst(
                    ClaimTypes.NameIdentifier).Value);

            var assignments =
                await _setterService
                    .GetAssignmentsAsync(setterUserId);

            ViewBag.Assignments = assignments;
            ViewBag.Mode = NormalizeUploadMode(mode);

            return View(new QuestionUploadViewModel { AssignmentId = assignmentId });
        }

        private static string NormalizeUploadMode(string mode)
        {
            var value = (mode ?? "").Trim().ToLowerInvariant();
            return value == "excel" || value == "word" ? value : null;
        }

        // Personal (personnel) number of the logged-in Setter: used as QB_Maker in the templates.
        private string GetPersonnelNo()
        {
            var claim = User.FindFirst(AuthClaimTypes.PersonnelNo);
            return claim == null ? "" : (claim.Value ?? "").Trim();
        }

        // Template file name follows the hierarchy chosen in the TOS assignment
        // (Exam / Paper / Section / SubSection / Topic - only the levels the assignment covers).
        private static string BuildTemplateFileName(SetterAssignmentViewModel assignment, string extension)
        {
            var parts = new List<string>();
            var level = (assignment.AssignmentLevel ?? "").Trim().ToUpperInvariant();

            parts.Add(assignment.ExamName);
            if (level == "PAPER" || level == "SECTION" || level == "SUBSECTION" || level == "TOPIC") parts.Add(assignment.PaperName);
            if (level == "SECTION" || level == "SUBSECTION" || level == "TOPIC") parts.Add(assignment.SectionName);
            if (level == "SUBSECTION" || level == "TOPIC") parts.Add(assignment.SubsectionName);
            if (level == "TOPIC") parts.Add(assignment.TopicName);

            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = parts
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p =>
                {
                    var chars = p.Trim().Select(ch => char.IsWhiteSpace(ch) || invalid.Contains(ch) ? '_' : ch).ToArray();
                    var text = new string(chars);
                    while (text.Contains("__")) text = text.Replace("__", "_");
                    return text.Trim('_');
                })
                .Where(p => p.Length > 0)
                .ToList();

            var name = cleaned.Count == 0 ? "TOS" : string.Join("_", cleaned);
            if (name.Length > 150) name = name.Substring(0, 150).TrimEnd('_');

            return name + "_Template." + extension;
        }

        [HttpGet]
        public async Task<IActionResult> DownloadTosExcelTemplate(long assignmentId)
        {
            long setterUserId = (long)GetCurrentUserId();
            var assignments = await _setterService.GetAssignmentsAsync(setterUserId);
            var assignment = assignments.FirstOrDefault(a => a.AssignmentId == assignmentId && a.IsActive);
            if (assignment == null) return Forbid();

            const string sql = @"
SELECT t.main_exam_name, t.paper_name, t.section_name, t.subsection_name,
       t.topic_name, t.topic_type, t.topic_easy_question, t.topic_easy_marks,
       t.topic_medium_question, t.topic_medium_mark, t.topic_hard_question,
       t.topic_hard_marks, t.main_exam_id, t.paper_id, t.section_id,
       t.subsection_id, t.topic_id,
       (SELECT COUNT(*) FROM question q WHERE q.assignment_id=a.assignment_id AND q.created_by=a.setter_user_id AND q.topic_id=t.topic_id AND q.difficulty_level='EASY') AS easy_created,
       (SELECT COUNT(*) FROM question q WHERE q.assignment_id=a.assignment_id AND q.created_by=a.setter_user_id AND q.topic_id=t.topic_id AND q.difficulty_level='MEDIUM') AS medium_created,
       (SELECT COUNT(*) FROM question q WHERE q.assignment_id=a.assignment_id AND q.created_by=a.setter_user_id AND q.topic_id=t.topic_id AND q.difficulty_level='HARD') AS hard_created
FROM tos t
JOIN question_assignment a ON a.assignment_id = @assignment_id
WHERE a.setter_user_id = @setter_id AND a.is_active = TRUE
  AND t.is_active = 1 AND t.main_exam_id = a.main_exam_id
  AND ((a.assignment_level = 'EXAM')
    OR (a.assignment_level = 'PAPER' AND t.paper_id = a.paper_id)
    OR (a.assignment_level = 'SECTION' AND t.paper_id = a.paper_id AND t.section_id = a.section_id)
    OR (a.assignment_level = 'SUBSECTION' AND t.paper_id = a.paper_id AND t.section_id = a.section_id AND t.subsection_id = a.subsection_id)
    OR (a.assignment_level = 'TOPIC' AND t.paper_id = a.paper_id AND t.section_id = a.section_id AND t.subsection_id IS NOT DISTINCT FROM a.subsection_id AND t.topic_id = a.topic_id))
ORDER BY t.main_exam_name, t.paper_name, t.section_name, t.subsection_name, t.topic_name;";

            string personnelNo = GetPersonnelNo();
            if (string.IsNullOrWhiteSpace(personnelNo)) personnelNo = setterUserId.ToString();

            var rows = new List<string[]>();
            rows.Add(new[] { "S.NO", "Examination", "Paper Name", "Section_Name", "SUBSECTION_Name", "Topic_name", "difficulty_level", "Question_type", "question_text", "Option-1", "Option-2", "Option-3", "Option-4", "Question Mark", "Correct_Answer", "QB_Maker" });
            int serial = 1;
            using (var connection = new NpgsqlConnection(_qbmsConnectionString))
            {
                await connection.OpenAsync();
                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("assignment_id", assignmentId);
                    command.Parameters.AddWithValue("setter_id", setterUserId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string[] difficulties = { "EASY", "MEDIUM", "HARD" };
                            int[] counts = { reader.IsDBNull(6) ? 0 : reader.GetInt32(6), reader.IsDBNull(8) ? 0 : reader.GetInt32(8), reader.IsDBNull(10) ? 0 : reader.GetInt32(10) };
                            int[] created = { Convert.ToInt32(reader.GetInt64(17)), Convert.ToInt32(reader.GetInt64(18)), Convert.ToInt32(reader.GetInt64(19)) };
                            int[] marks = { reader.IsDBNull(7) ? 0 : reader.GetInt32(7), reader.IsDBNull(9) ? 0 : reader.GetInt32(9), reader.IsDBNull(11) ? 0 : reader.GetInt32(11) };
                            string questionType = NormalizeTosQuestionType(reader.IsDBNull(5) ? "" : reader.GetString(5));
                            for (int d = 0; d < 3; d++)
                            {
                                int required = Math.Max(0, (int)Math.Ceiling(counts[d] * assignment.MultiplicationFactor) - created[d]);
                                for (int i = 0; i < required; i++)
                                {
                                    rows.Add(new[] { (serial++).ToString(), reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? "" : reader.GetString(3), reader.GetString(4), difficulties[d], questionType, "", "", "", "", "", marks[d].ToString(), "", personnelNo });
                                }
                            }
                        }
                    }
                }
            }
            if (rows.Count == 1)
            {
                TempData["ErrorMessage"] = "No active TOS question requirements were found for this assignment.";
                return RedirectToAction(nameof(UploadQuestions));
            }
            var bytes = TosExcelHelper.CreateWorkbook(rows);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", BuildTemplateFileName(assignment, "xlsx"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadQuestions(long assignmentId, IFormFile file)
        {
            long setterUserId = (long)GetCurrentUserId();
            var assignments = await _setterService.GetAssignmentsAsync(setterUserId);
            ViewBag.Assignments = assignments;
            ViewBag.Mode = "excel";
            var model = new QuestionUploadViewModel { AssignmentId = assignmentId };
            var assignment = assignments == null ? null : assignments.FirstOrDefault(a => a.AssignmentId == assignmentId && a.IsActive);
            if (assignment == null)
            {
                model.Errors.Add("Select a valid active assignment.");
                return View(model);
            }
            if (file == null || file.Length == 0 || !string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                model.Errors.Add("Please select the completed e-Prashnalaya TOS Excel template (.xlsx).");
                return View(model);
            }
            List<List<string>> sheet;
            try { sheet = TosExcelHelper.ReadFirstWorksheet(file.OpenReadStream()); }
            catch (Exception ex) { model.Errors.Add("Unable to read Excel file: " + ex.Message); return View(model); }

            string[] headers = { "S.NO", "Examination", "Paper Name", "Section_Name", "SUBSECTION_Name", "Topic_name", "difficulty_level", "Question_type", "question_text", "Option-1", "Option-2", "Option-3", "Option-4", "Question Mark", "Correct_Answer", "QB_Maker" };
            // Extra columns after the 16 template columns (e.g. the duplicate-check result columns) are ignored,
            // so the downloaded result file can be corrected and uploaded again.
            if (sheet.Count < 2 || sheet[0].Count < headers.Length || !headers.Select((h, i) => string.Equals((sheet[0][i] ?? "").Trim(), h, StringComparison.OrdinalIgnoreCase)).All(x => x))
            {
                model.Errors.Add("The Excel headers do not match the e-Prashnalaya TOS template. Download a fresh template and do not rename or reorder the first 16 columns.");
                return View(model);
            }

            var rows = new List<UploadRowInput>();
            int rowNumber = 1;
            foreach (var cells in sheet.Skip(1))
            {
                rowNumber++;
                var first16 = Enumerable.Range(0, headers.Length).Select(i => i < cells.Count ? (cells[i] ?? "").Trim() : "").ToArray();
                if (first16.All(string.IsNullOrWhiteSpace)) continue;
                rows.Add(new UploadRowInput { RowNumber = rowNumber, Cells = first16, Html = null });
            }
            model.FileName = Path.GetFileName(file.FileName);

            var batchItems = new List<UploadBatchItemModel>();
            if (!await ValidateUploadRowsAsync(model, assignment, setterUserId, rows, batchItems))
                return View("UploadQuestions", model);

            // Nothing is written to the question table yet. The file is staged and the duplicate
            // check runs in the background; questions are saved and sent to the Vetter only if it clears.
            await _uploadBatchRepository.CreateBatchAsync(assignmentId, setterUserId, file.FileName, "XLSX", sheet[0].Take(headers.Length).ToArray(), batchItems);
            TempData["SuccessMessage"] = UploadAcceptedMessage(batchItems.Count);
            return RedirectToAction(nameof(Dashboard));
        }

        private static string UploadAcceptedMessage(int count)
        {
            return "File uploaded and validated successfully (" + count + " question(s)). The duplicate check is now running - please come back to this dashboard and check the Duplicate Check Status below. Questions are saved and sent to the Vetter only after the check is cleared.";
        }

        private sealed class UploadRowInput
        {
            public int RowNumber;
            public string[] Cells;   // the 16 template columns
            public string[] Html;    // same columns as HTML (Word only); null for Excel
        }

        private static string PlainToHtml(string text)
        {
            return System.Net.WebUtility.HtmlEncode(text ?? "").Replace("\r\n", "\n").Replace("\n", "<br>");
        }

        // ONE set of rules for Excel and Word uploads. Every rule that used to exist in only one of the two
        // paths is applied to both: TOS hierarchy, difficulty, question type, marks, QB_Maker, question text,
        // MCQ options + correct answer, in-file duplicates, per-topic quota and total quota.
        private async Task<bool> ValidateUploadRowsAsync(QuestionUploadViewModel model, SetterAssignmentViewModel assignment, long setterUserId, IList<UploadRowInput> rows, List<UploadBatchItemModel> batchItems)
        {
            var tosRows = (await _tosService.GetAllAsync() ?? new List<TosViewModel>())
                .Where(t => t.IsActive == 1 && IsInAssignmentScope(t, assignment)).ToList();
            var created = await GetCreatedCountsAsync(assignment.AssignmentId, setterUserId);
            string maker = GetPersonnelNo();
            if (string.IsNullOrWhiteSpace(maker)) maker = setterUserId.ToString();
            // templates downloaded before this change carried the Setter's name; keep accepting that
            string legacyMaker = User.Identity != null ? User.Identity.Name : null;

            var seenQuestions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var perTopicCount = new Dictionary<string, int>();
            var perTopicName = new Dictionary<string, string>();
            var parsed = new List<CreateQuestionViewModel>();
            model.TotalRows = rows.Count;

            foreach (var row in rows)
            {
                var c = row.Cells;
                bool isWord = row.Html != null;
                Func<int, string> T = i => i < c.Length ? (c[i] ?? "").Trim() : "";
                Func<int, string> H = i => isWord ? (i < row.Html.Length ? row.Html[i] ?? "" : "") : PlainToHtml(T(i));
                Func<int, bool> Blank = i => string.IsNullOrWhiteSpace(T(i)) && H(i).IndexOf("<img", StringComparison.OrdinalIgnoreCase) < 0;

                var preview = new UploadedQuestionModel
                {
                    RowNumber = row.RowNumber, ExamName = T(1), PaperName = T(2), SectionName = T(3), SubsectionName = T(4), TopicName = T(5),
                    DifficultyLevel = T(6).ToUpperInvariant(), QuestionType = T(7).ToUpperInvariant(), QuestionText = T(8), QuestionHtml = H(8)
                };

                string error = null;
                TosViewModel topic = tosRows.FirstOrDefault(t => Eq(t.ExamName, T(1)) && Eq(t.PaperName, T(2)) && Eq(t.SectionName, T(3)) && Eq(t.SubsectionName, T(4)) && Eq(t.TopicName, T(5)));
                string type = null;
                string difficulty = T(6).ToUpperInvariant();

                if (topic == null) error = "TOS hierarchy (Examination / Paper / Section / SubSection / Topic) does not match this Setter assignment.";
                else if (!new[] { "EASY", "MEDIUM", "HARD" }.Contains(difficulty)) error = "Difficulty must be EASY, MEDIUM or HARD.";
                else if (!Eq(T(15), maker) && !(legacyMaker != null && Eq(T(15), legacyMaker))) error = "QB_Maker must be your personal number and must not be changed.";
                else
                {
                    try { type = NormalizeTosQuestionType(topic.TopicType); }
                    catch (InvalidOperationException ex) { error = ex.Message; }
                    if (error == null && !TypesMatch(T(7), topic.TopicType)) error = "Question type does not match the TOS topic type.";
                }

                if (error == null)
                {
                    int expectedMarks = difficulty == "EASY" ? topic.TopicEasyMarks : difficulty == "MEDIUM" ? topic.TopicMediumMarks : topic.TopicHardMarks;
                    int actualMarks;
                    if (!int.TryParse(T(13), out actualMarks) || actualMarks != expectedMarks) error = "Question Mark does not match the TOS marks for this difficulty.";
                }

                if (error == null)
                {
                    if (Blank(8)) error = "Question text is required.";
                    else if (T(8).Length > 50000) error = "Question text exceeds 50,000 characters.";
                    else if (type == "OBJ" && (Blank(9) || Blank(10) || Blank(11) || Blank(12))) error = "MCQ rows require all four options.";
                    else if (type == "OBJ" && string.IsNullOrWhiteSpace(T(14))) error = "Correct_Answer is required for MCQ.";
                    else if (type == "OBJ" && !IsValidCorrectAnswer(T(14), T(9), T(10), T(11), T(12))) error = "Correct_Answer must be 1-4, Option 1-Option 4, A-D, or exactly match one of the four options.";
                }

                if (error == null)
                {
                    // Same text twice in the file (HTML, spacing and case ignored).
                    var plain = QBMS.Infrastructure.Services.QuestionContentHelper.Extract(H(8), null);
                    var key = NormalizeQuestionText(plain).ToLowerInvariant();
                    if (key.Length > 0 && !seenQuestions.Add(key)) error = "Duplicate question text appears more than once in this file.";
                }

                if (error != null)
                {
                    AddUploadError(model, preview, error);
                    continue;
                }

                string topicKey = topic.TopicId + "|" + difficulty;
                int already; perTopicCount.TryGetValue(topicKey, out already);
                perTopicCount[topicKey] = already + 1;
                perTopicName[topicKey] = topic.TopicName;

                var question = new CreateQuestionViewModel
                {
                    AssignmentID = assignment.AssignmentId,
                    MainExamId = topic.ExamId, PaperId = topic.PaperId, SectionId = topic.SectionId, SubsectionId = topic.SubsectionId, TopicId = topic.TopicId,
                    ExamName = topic.ExamName, PaperName = topic.PaperName, SectionName = topic.SectionName, SubsectionName = topic.SubsectionName, TopicName = topic.TopicName,
                    QuestionText = H(8), QuestionType = type, DifficultyLevel = difficulty,
                    Option1 = type == "OBJ" ? H(9) : "", Option2 = type == "OBJ" ? H(10) : "", Option3 = type == "OBJ" ? H(11) : "", Option4 = type == "OBJ" ? H(12) : "",
                    CorrectAnswer = type == "OBJ" ? NormalizeCorrectAnswer(T(14)) : ""
                };
                parsed.Add(question);
                batchItems.Add(new UploadBatchItemModel { RowNumber = row.RowNumber, Question = question, RowCells = c.Select(x => x ?? "").ToArray() });
                preview.IsValid = true;
                preview.ErrorMessage = "Validated";
                model.Questions.Add(preview);
            }

            model.ValidRows = model.Questions.Count(q => q.IsValid);
            model.InvalidRows = model.TotalRows - model.ValidRows;
            if (model.TotalRows == 0) model.Errors.Add("The file contains no question rows.");
            if (model.InvalidRows > 0) model.Errors.Add("No questions were saved. Correct all row errors and upload the file again.");
            if (model.TotalRows == 0 || model.InvalidRows > 0) return false;

            // Per topic and difficulty: not more questions than the TOS still requires for this assignment.
            var overflow = new List<string>();
            foreach (var entry in perTopicCount)
            {
                var parts = entry.Key.Split('|');
                long topicId = long.Parse(parts[0]);
                var t = tosRows.First(x => x.TopicId == topicId);
                int perTopic = parts[1] == "EASY" ? t.TopicEasyQuestion : parts[1] == "MEDIUM" ? t.TopicMediumQuestion : t.TopicHardQuestion;
                int required = (int)Math.Ceiling(Math.Max(0, perTopic) * assignment.MultiplicationFactor);
                int done; created.TryGetValue(entry.Key, out done);
                int remaining = Math.Max(0, required - done);
                if (entry.Value > remaining)
                    overflow.Add(perTopicName[entry.Key] + " / " + parts[1] + ": file has " + entry.Value + ", remaining " + remaining);
            }
            if (overflow.Count > 0)
            {
                model.Errors.Add("More questions than the TOS requires (" + string.Join("; ", overflow) + ").");
                model.Errors.Add("No questions were saved. Reduce the questions in the file and upload it again.");
                return false;
            }

            // Whole-file quota (includes questions already staged by other uploads).
            var quotaError = await CheckUploadQuotaAsync(assignment.AssignmentId, setterUserId, parsed);
            if (quotaError != null)
            {
                model.Errors.Add(quotaError);
                model.Errors.Add("No questions were saved. Reduce the questions in the file and upload it again.");
                return false;
            }
            return true;
        }

        private static bool Eq(string a, string b) => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
        private static bool TypesMatch(string excelType, string tosType)
        {
            try
            {
                return string.Equals(
                    NormalizeTosQuestionType(excelType),
                    NormalizeTosQuestionType(tosType),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }


        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //    public async Task<IActionResult> UploadQuestions(
        //QuestionUploadViewModel model)
        //    {
        //        long setterUserId = long.Parse(
        //            User.FindFirst(
        //                ClaimTypes.NameIdentifier).Value);

        //        var assignments =
        //            await _setterService
        //                .GetAssignmentsAsync(setterUserId);

        //        ViewBag.Assignments = assignments;

        //        if (model.File == null)
        //        {
        //            ModelState.AddModelError(
        //                "File",
        //                "Please select a file.");

        //            return View(model);
        //        }

        //        model =
        //            await _questionUploadService
        //                .ProcessFileAsync(
        //                    model,
        //                    setterUserId);

        //        if (model.InvalidRows > 0)
        //        {
        //            return View(model);
        //        }

        //        var saved =
        //            await _questionUploadService
        //                .SaveQuestionsAsync(
        //                    model,
        //                    setterUserId);

        //        if (!saved)
        //        {
        //            model.Errors.Add(
        //                "Unable to save uploaded questions.");

        //            return View(model);
        //        }

        //        TempData["Success"] =
        //            "Questions uploaded successfully.";

        //        return RedirectToAction("Dashboard");
        //    }    //    public async Task<IActionResult> UploadQuestions(
        //QuestionUploadViewModel model)
        //    {
        //        long setterUserId = long.Parse(
        //            User.FindFirst(
        //                ClaimTypes.NameIdentifier).Value);

        //        var assignments =
        //            await _setterService
        //                .GetAssignmentsAsync(setterUserId);

        //        ViewBag.Assignments = assignments;

        //        if (model.File == null)
        //        {
        //            ModelState.AddModelError(
        //                "File",
        //                "Please select a file.");

        //            return View(model);
        //        }

        //        model =
        //            await _questionUploadService
        //                .ProcessFileAsync(
        //                    model,
        //                    setterUserId);

        //        if (model.InvalidRows > 0)
        //        {
        //            return View(model);
        //        }

        //        var saved =
        //            await _questionUploadService
        //                .SaveQuestionsAsync(
        //                    model,
        //                    setterUserId);

        //        if (!saved)
        //        {
        //            model.Errors.Add(
        //                "Unable to save uploaded questions.");

        //            return View(model);
        //        }

        //        TempData["Success"] =
        //            "Questions uploaded successfully.";

        //        return RedirectToAction("Dashboard");
        //    }

        [HttpGet]
        public async Task<IActionResult> DownloadTosWordTemplate(long assignmentId)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            try
            {
                var assignment = await GetOwnedAssignmentAsync(assignmentId, userId.Value);
                if (assignment == null) return Forbid();

                var tosRows = await _tosService.GetAllAsync();
                var created = await GetCreatedCountsAsync(assignmentId, userId.Value);
                var rows = BuildWordTemplateRows(assignment, tosRows, GetPersonnelNo(), created);
                if (rows.Count == 0)
                {
                    TempData["ErrorMessage"] = "No question requirements remain for this assignment (no active TOS rows, or its quota is already created).";
                    return RedirectToAction(nameof(UploadQuestions));
                }

                var bytes = TosWordTemplateHelper.CreateDocument(rows, "e-Prashnalaya TOS Question Template");
                return File(bytes,
                    "application/msword",
                    BuildTemplateFileName(assignment, "doc"));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Unable to generate Word template: " + ex.Message;
                return RedirectToAction(nameof(UploadQuestions));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(15728640)]
        public async Task<IActionResult> UploadWordQuestions(long assignmentId, IFormFile uploadedFile)
        {
            var userId = GetCurrentUserId();
            var model = new QuestionUploadViewModel { AssignmentId = assignmentId };
            if (!userId.HasValue) return RedirectToAction("Login", "Account");
            var assignments = await _setterService.GetAssignmentsAsync(userId.Value);
            ViewBag.Assignments = assignments;
            ViewBag.Mode = "word";
            var assignment = assignments == null ? null : assignments.FirstOrDefault(a => a.AssignmentId == assignmentId && a.IsActive);
            if (assignment == null)
            {
                model.Errors.Add("Select a valid active assignment that belongs to the logged-in Setter.");
                return View("UploadQuestions", model);
            }
            if (uploadedFile == null || uploadedFile.Length == 0)
            {
                model.Errors.Add("Select a non-empty Word .doc file.");
                return View("UploadQuestions", model);
            }
            if (uploadedFile.Length > 15 * 1024 * 1024)
            {
                model.Errors.Add("The file exceeds the 15 MB upload limit.");
                return View("UploadQuestions", model);
            }
            if (!string.Equals(Path.GetExtension(uploadedFile.FileName), ".doc", StringComparison.OrdinalIgnoreCase))
            {
                model.Errors.Add("Only .doc Word documents are supported. Fill in the Word template downloaded from e-Prashnalaya and upload it with its .doc extension.");
                return View("UploadQuestions", model);
            }

            try
            {
                using (var stream = new MemoryStream())
                {
                    await uploadedFile.CopyToAsync(stream);
                    var signature = stream.ToArray();
                    if (signature.Length >= 4 && signature[0] == 0xD0 && signature[1] == 0xCF && signature[2] == 0x11 && signature[3] == 0xE0)
                        throw new InvalidDataException("This file was saved in the old binary Word 97-2003 format, which cannot be read. Download the e-Prashnalaya Word template again, fill it in without changing the file format, and upload it.");
                    if (signature.Length < 4 || signature[0] != 0x50 || signature[1] != 0x4B)
                        throw new InvalidDataException("The uploaded file is not a valid e-Prashnalaya Word template. Download the template from e-Prashnalaya, fill it in and upload it.");

                    stream.Position = 0;
                    var tableRows = TosWordTemplateHelper.ReadFirstTable(stream);
                    if (tableRows.Count < 2) throw new InvalidDataException("The Word table has no question rows.");

                    // The first 16 columns must be the template. Columns after them (the duplicate-check result
                    // columns Duplicate_Status / Top_Match_1..3 of a downloaded result file) are ignored, so a
                    // corrected result file can be uploaded again. Before, any extra column was rejected.
                    var header = tableRows[0];
                    int templateColumns = TosWordTemplateHelper.Headers.Length;
                    if (header.Length < templateColumns ||
                        !TosWordTemplateHelper.Headers.Select((h, i) => string.Equals((header[i] ?? "").Trim(), h, StringComparison.OrdinalIgnoreCase)).All(x => x))
                        throw new InvalidDataException("The table columns do not match the e-Prashnalaya TOS template. Download a fresh template and do not change the names or order of the first 16 columns.");

                    stream.Position = 0;
                    var tableHtml = TosWordTemplateHelper.ReadFirstTableHtml(stream);

                    var rows = new List<UploadRowInput>();
                    for (int ix = 1; ix < tableRows.Count; ix++)
                    {
                        var cells = tableRows[ix];
                        if (cells.All(x => string.IsNullOrWhiteSpace(x))) continue;
                        var htmlCells = ix < tableHtml.Count ? tableHtml[ix] : new string[0];
                        if (cells.Length < templateColumns)
                        {
                            AddUploadError(model, new UploadedQuestionModel { RowNumber = ix + 1 }, "This row has " + cells.Length + " cells; at least " + templateColumns + " are required.");
                            continue;
                        }
                        rows.Add(new UploadRowInput
                        {
                            RowNumber = ix + 1,
                            Cells = cells.Take(templateColumns).Select(x => (x ?? "").Trim()).ToArray(),
                            Html = Enumerable.Range(0, templateColumns).Select(i => i < htmlCells.Length ? htmlCells[i] : "").ToArray()
                        });
                    }
                    model.FileName = Path.GetFileName(uploadedFile.FileName);

                    var batchItems = new List<UploadBatchItemModel>();
                    bool structureOk = model.Errors.Count == 0;
                    if (!await ValidateUploadRowsAsync(model, assignment, userId.Value, rows, batchItems) || !structureOk)
                    {
                        if (model.Errors.Count == 0) model.Errors.Add("Not all rows passed validation. No questions were saved.");
                        return View("UploadQuestions", model);
                    }

                    await _uploadBatchRepository.CreateBatchAsync(assignmentId, userId.Value, model.FileName, "DOCX", header.Take(templateColumns).ToArray(), batchItems, signature);
                    TempData["SuccessMessage"] = UploadAcceptedMessage(batchItems.Count);
                    return RedirectToAction(nameof(Dashboard));
                }
            }
            catch (InvalidDataException ex)
            {
                model.Errors.Add(ex.Message);
                return View("UploadQuestions", model);
            }
            catch (InvalidOperationException ex)
            {
                model.Errors.Add("The document could not be processed: " + ex.Message);
                return View("UploadQuestions", model);
            }
            catch (Exception ex)
            {
                model.Errors.Add("Upload failed. Check the Word document and try again. Details: " + ex.Message);
                return View("UploadQuestions", model);
            }
        }

        private async Task<SetterAssignmentViewModel> GetOwnedAssignmentAsync(long assignmentId, long setterUserId)
        {
            var assignments = await _setterService.GetAssignmentsAsync(setterUserId);
            return assignments == null ? null : assignments.FirstOrDefault(a => a.AssignmentId == assignmentId);
        }

        // Questions already created for this assignment, per topic and difficulty (key "topicId|EASY"). Same basis as the Excel template.
        private async Task<Dictionary<string, int>> GetCreatedCountsAsync(long assignmentId, long setterUserId)
        {
            const string sql = @"
SELECT topic_id, difficulty_level, COUNT(*)
FROM question
WHERE assignment_id = @assignment_id AND created_by = @setter_id
GROUP BY topic_id, difficulty_level;";

            var created = new Dictionary<string, int>();
            using (var connection = new NpgsqlConnection(_qbmsConnectionString))
            {
                await connection.OpenAsync();
                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("assignment_id", assignmentId);
                    command.Parameters.AddWithValue("setter_id", setterUserId);
                    using (var reader = await command.ExecuteReaderAsync())
                        while (await reader.ReadAsync())
                            created[reader.GetInt64(0) + "|" + reader.GetString(1).ToUpperInvariant()] = Convert.ToInt32(reader.GetInt64(2));
                }
            }
            return created;
        }

        private static List<string[]> BuildWordTemplateRows(SetterAssignmentViewModel assignment, IList<TosViewModel> tosRows, string maker, IDictionary<string, int> created)
        {
            var selected = (tosRows ?? new List<TosViewModel>()).Where(t => t.IsActive == 1 && IsInAssignmentScope(t, assignment))
                .OrderBy(t => t.ExamName).ThenBy(t => t.PaperName).ThenBy(t => t.SectionName).ThenBy(t => t.SubsectionName).ThenBy(t => t.TopicName).ToList();
            var rows = new List<string[]>();
            int serial = 1;
            foreach (var topic in selected)
            {
                var type = NormalizeTosQuestionType(topic.TopicType);
                var levels = new[]
                {
                    new { Name = "EASY", Count = topic.TopicEasyQuestion, Marks = topic.TopicEasyMarks },
                    new { Name = "MEDIUM", Count = topic.TopicMediumQuestion, Marks = topic.TopicMediumMarks },
                    new { Name = "HARD", Count = topic.TopicHardQuestion, Marks = topic.TopicHardMarks }
                };
                foreach (var level in levels)
                {
                    var count = (int)Math.Ceiling(Math.Max(0, level.Count) * assignment.MultiplicationFactor);
                    int alreadyCreated;
                    if (created != null && created.TryGetValue(topic.TopicId + "|" + level.Name, out alreadyCreated)) count = Math.Max(0, count - alreadyCreated);
                    for (int i = 0; i < count; i++)
                    {
                        rows.Add(new[]
                        {
                            serial.ToString(), topic.ExamName ?? "", topic.PaperName ?? "", topic.SectionName ?? "",
                            topic.SubsectionName ?? "", topic.TopicName ?? "", level.Name, type,
                            "", "", "", "", "", level.Marks.ToString(), "", maker ?? ""
                        });
                        serial++;
                    }
                }
            }
            return rows;
        }

        private static bool IsInAssignmentScope(TosViewModel topic, SetterAssignmentViewModel assignment)
        {
            if (topic.ExamId != assignment.MainExamId) return false;
            var level = (assignment.AssignmentLevel ?? "").Trim().ToUpperInvariant();
            if (level == "EXAM") return true;
            if (!assignment.PaperId.HasValue || topic.PaperId != assignment.PaperId.Value) return false;
            if (level == "PAPER") return true;
            if (!assignment.SectionId.HasValue || topic.SectionId != assignment.SectionId.Value) return false;
            if (level == "SECTION") return true;
            if (level == "SUBSECTION") return assignment.SubsectionId.HasValue && topic.SubsectionId == assignment.SubsectionId;
            // TOPIC: the sub-section may be empty (same rule as the SQL "IS NOT DISTINCT FROM" used for the Excel template)
            if (level == "TOPIC") return assignment.TopicId.HasValue && topic.TopicId == assignment.TopicId.Value && topic.SubsectionId == assignment.SubsectionId;
            return false;
        }
        private static string NormalizeTosQuestionType(string value)
        {
            var type = (value ?? "").Trim().ToUpperInvariant().Replace("_", " ").Replace("-", " ");
            if (new[] { "OBJ", "M", "OBJ", "O", "OBJECTIVE", "MULTIPLE CHOICE", "MULTIPLE CHOICE QUESTION" }.Contains(type)) return "OBJ";
            if (new[] { "SUBJECTIVE", "SUB", "S", "DESCRIPTIVE", "DESCRIPTIVE QUESTION", "D" }.Contains(type)) return "SUBJECTIVE";
            throw new InvalidOperationException("Unknown TOS TOPIC_TYPE value '" + value + "'. Update the question-type mapping before generating the template.");
        }

        private static bool IsValidCorrectAnswer(string answer, string option1, string option2, string option3, string option4)
        {
            var value = (answer ?? "").Trim();
            if (new[] { "1", "2", "3", "4", "OPTION 1", "OPTION 2", "OPTION 3", "OPTION 4", "A", "B", "C", "D" }.Contains(value.ToUpperInvariant())) return true;
            return new[] { option1, option2, option3, option4 }.Any(o => !string.IsNullOrWhiteSpace(o) && string.Equals(o.Trim(), value, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeCorrectAnswer(string answer)
        {
            var value = (answer ?? "").Trim().ToUpperInvariant();
            if (value == "A") return "Option 1";
            if (value == "B") return "Option 2";
            if (value == "C") return "Option 3";
            if (value == "D") return "Option 4";
            if (value == "1" || value == "2" || value == "3" || value == "4") return "Option " + value;
            return answer ?? "";
        }

        private static string NormalizeQuestionText(string text)
        {
            return string.Join(" ", (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).Trim();
        }

        private static void AddUploadError(QuestionUploadViewModel model, UploadedQuestionModel preview, string message)
        {
            preview.IsValid = false;
            preview.ErrorMessage = message;
            model.Questions.Add(preview);
            model.Errors.Add("Row " + preview.RowNumber + ": " + message);
        }

        // Whole-file quota check. The existing quota service is the source of truth; questions that are
        // staged in other batches still waiting for their duplicate check are reserved against it.
        // Returns null when the file fits, otherwise a message for the Setter.
        private async Task<string> CheckUploadQuotaAsync(long assignmentId, long setterUserId, List<CreateQuestionViewModel> questions)
        {
            var quota = await _quotaService.GetQuotaAsync(assignmentId, setterUserId);
            if (quota == null) return "Question quota could not be determined for this assignment.";

            var reserved = await _uploadBatchRepository.GetReservedCountsAsync(assignmentId, setterUserId, 0);

            int easyInFile = questions.Count(x => string.Equals(x.DifficultyLevel, "EASY", StringComparison.OrdinalIgnoreCase));
            int mediumInFile = questions.Count(x => string.Equals(x.DifficultyLevel, "MEDIUM", StringComparison.OrdinalIgnoreCase));
            int hardInFile = questions.Count(x => string.Equals(x.DifficultyLevel, "HARD", StringComparison.OrdinalIgnoreCase));

            int easyAvailable = Math.Max(0, quota.Easyremaining - reserved.easy);
            int mediumAvailable = Math.Max(0, quota.Mediumremaining - reserved.medium);
            int hardAvailable = Math.Max(0, quota.Hardremaining - reserved.hard);

            var problems = new List<string>();
            if (easyInFile > easyAvailable) problems.Add("EASY: file has " + easyInFile + ", remaining quota " + easyAvailable);
            if (mediumInFile > mediumAvailable) problems.Add("MEDIUM: file has " + mediumInFile + ", remaining quota " + mediumAvailable);
            if (hardInFile > hardAvailable) problems.Add("HARD: file has " + hardInFile + ", remaining quota " + hardAvailable);

            return problems.Count == 0 ? null : "Question quota exceeded (" + string.Join("; ", problems) + ").";
        }

        // Result file: the uploaded Word/Excel again, with duplicate status and top 3 matches per question.
        [HttpGet]
        public async Task<IActionResult> DownloadDuplicateReport(long batchId)
        {
            long setterUserId = (long)GetCurrentUserId();
            var batch = await _uploadBatchRepository.GetBatchAsync(batchId, setterUserId);
            if (batch == null) return NotFound();

            if (!batch.IsReportReady)
            {
                TempData["ErrorMessage"] = "The duplicate check is not complete yet. The result file is available at 100%.";
                return RedirectToAction(nameof(Dashboard));
            }

            var extraHeaders = new[]
            {
                "Duplicate_Status",
                "Top_Match_1 (Similarity | Question)",
                "Top_Match_2 (Similarity | Question)",
                "Top_Match_3 (Similarity | Question)"
            };
            var headerRow = batch.HeaderRow.Concat(extraHeaders).ToArray();

            var rows = new List<string[]> { headerRow };
            var extraRows = new List<string[]>();
            foreach (var item in batch.Items)
            {
                var cells = new string[batch.HeaderRow.Length];
                for (int i = 0; i < cells.Length; i++) cells[i] = i < item.RowCells.Length ? item.RowCells[i] : "";

                var extra = new string[extraHeaders.Length];
                extra[0] = item.Matches.Count == 0
                    ? item.CheckStatus + " - no similar question found"
                    : item.CheckStatus + " - " + item.TopScore.ToString("0.00") + "%";
                for (int m = 0; m < 3; m++)
                {
                    if (item.Matches[m].SimilarityPercent>85)
                    {
                        extra[m + 1] = m >= item.Matches.Count ? ""
                       : item.Matches[m].FileRowNumber > 0
                           ? "Row " + item.Matches[m].FileRowNumber + " of this file | " + item.Matches[m].SimilarityPercent.ToString("0.00") + "% | " + item.Matches[m].QuestionText
                           : item.Matches[m].SimilarityPercent.ToString("0.00") + "% | " + item.Matches[m].QuestionText;
                    }
                    else
                    {
                        extra[m + 1] = "";
                    }
                   
                }
                extraRows.Add(extra);
                rows.Add(cells.Concat(extra).ToArray());
            }

            if (string.Equals(batch.FileType, "DOCX", StringComparison.OrdinalIgnoreCase))
            {
                // Preferred: the Setter's own document with the result columns added, so pictures, equations
                // and layout are exactly as uploaded. The rebuilt text-only table is only a fallback.
                var wordBytes = batch.FileData != null
                    ? TosWordTemplateHelper.AppendColumns(batch.FileData, extraHeaders, extraRows)
                    : TosWordTemplateHelper.CreateDocument(rows.Skip(1).ToList(), "e-Prashnalaya Duplicate Check Result", headerRow, false);
                return File(wordBytes, "application/msword", "e-Prashnalaya_Duplicate_Check_Result_" + batch.BatchId + ".doc");
            }

            var excelBytes = TosExcelHelper.CreateWorkbook(rows);
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "e-Prashnalaya_Duplicate_Check_Result_" + batch.BatchId + ".xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            long setterUserId = (long)GetCurrentUserId();


            var allAssignments =
                await _setterService.GetAssignmentsAsync(setterUserId);

            var pendingAssignments = new List<SetterAssignmentViewModel>();
            var completedAssignments = new List<SetterAssignmentViewModel>();

            // An assignment stays pending until its complete quota has been
            // created. The quota service is the source of truth.
            foreach (var assignment in allAssignments)
            {
                var quota = await _quotaService.GetQuotaAsync(
                    assignment.AssignmentId, setterUserId);

                // progress bar: questions created so far / questions the TOS quota requires
                if (quota != null)
                {
                    assignment.TotalQuota = quota.TotalAssigned;
                    assignment.CreatedQuota = quota.TotalUsed;
                }

                if (quota != null && quota.TotalRemaining > 0)
                    pendingAssignments.Add(assignment);
                else
                    completedAssignments.Add(assignment);
            }

            var allQuestions =
                await _setterService.GetQuestionsAsync(setterUserId);

            // Only rejected questions should appear on the Setter dashboard
            // for correction. Draft/submitted/approved questions are omitted.
            var rejectedQuestions = allQuestions == null
                ? new List<SetterQuestionViewModel>()
                : allQuestions.FindAll(q =>
                    string.Equals(q.Status, "VETTER_REJECTED",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(q.Status, "ADMIN_REJECTED",
                        StringComparison.OrdinalIgnoreCase));

            var model = new SetterDashboardViewModel
            {
                Assignments = pendingAssignments,
                CompletedAssignments = completedAssignments,
                Questions = rejectedQuestions,
                UploadBatches = await _uploadBatchRepository.GetBatchesAsync(setterUserId)
            };

            return View(model);
        }

        //private long GetCurrentUserId()
        //{
        //    if (!_currentUser.IsAuthenticated ||
        //        _currentUser.UserId <= 0)
        //    {
        //        throw new System.InvalidOperationException(
        //            "Logged-in user could not be identified.");
        //    }

        //    return _currentUser.UserId;
        //}


        [HttpGet]
        public async Task<IActionResult> CreateQuestion(long assignmentId, long? questionId = null, bool newQuestion = false)
        {
            long userId = (long)GetCurrentUserId();
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }
            try
            {
                var model1 = await _questionCreationService.GetCreatePageAsync(assignmentId, userId, questionId, newQuestion);
                if(model1==null)
                {
                    return NotFound("Assignment Not Found");
                }
                var a = TempData["ErrorMessage"];
                return View(model1);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "unable to load correct question page.";
                return RedirectToAction("Dashboard");
            }

            //var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            //if (claim == null)
            //{
            //    return RedirectToAction("Login", "Account");
            //}


            //if (!long.TryParse(claim.Value, out long setterUserId))
            //{
            //    return RedirectToAction("Login", "Account");
            //}


            //var topics = await _setterService.GetTopicsAsync(assignmentId, setterUserId);
            //if (topics == null || topics.Count == 0)
            //{
            //    TempData["ErrorMessage"] = "No Topics are available for this assignment";
            //}

            //var quota = await _quotaService.GetQuotaAsync(assignmentId, setterUserId);
            //var firstTopic = topics[0];
            //var model = new CreateQuestionPageViewModel
            //{
            //    AssignmentId = assignmentId,
            //    Topics = topics,
            //    Question = new CreateQuestionViewModel
            //    {
            //        MainExamId = firstTopic.MainExamId,
            //        PaperId = firstTopic.PaperId,
            //        SectionId = firstTopic.SectionId,
            //        SubsectionId = firstTopic.SubsectionId,
            //        TopicId = firstTopic.TopicId,
            //        ExamName = firstTopic.ExamName,
            //        PaperName = firstTopic.PaperName,
            //        SectionName = firstTopic.SectionName,
            //        SubsectionName = firstTopic.SubsectionName,
            //        TopicName = firstTopic.TopicName,
            //        Option1 = firstTopic.Option1,
            //        Option2 = firstTopic.Option2,
            //        Option3 = firstTopic.Option3,
            //        Option4 = firstTopic.Option4,
            //        CorrectAnswer = firstTopic.CorrectAnswer

            //    },
            //    QuestionTypes =
            //    new List<string>
            //    {
            //        "MCQ",
            //        "SUBJECTIVE"
            //    },
            //    Difficulties =
            //    new List<string>
            //    {
            //        "EASY",
            //        "MEDIUM",
            //        "HARD"
            //    },
            //    Quota = quota

            //};
            //return View(model);
        }
    



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuestion(CreateQuestionPageViewModel model, string navigationAction)
        {
        var userId = GetCurrentUserId();

            if(userId==null)
            {
                RedirectToAction("Login", "Account");
            }

            if(model==null || model.Question==null)
            {
                TempData["ErrorMessage"] = "Invalid question data.";
                return RedirectToAction("Dashboard");
            }
            model.NavigationAction = navigationAction;

            /////// ASSIGNMENT
            model.Question.AssignmentID = model.AssignmentId;

            // "MCQ" / "OBJ" / "Objective" are the same thing; the database stores OBJ.
            model.Question.QuestionType = QBMS.Infrastructure.Services.SetterQuestionCreationRepository.NormalizeQuestionType(model.Question.QuestionType);
            bool isObjective = model.Question.QuestionType == "OBJ";

            ///// VALIDATE SERVER SIDE

            if (string.IsNullOrWhiteSpace( model.Question.QuestionText))
            {
                ModelState.AddModelError( "Question.QuestionText", "Please enter the question.");
            }

            if (isObjective && string.IsNullOrWhiteSpace( model.Question.Option1))
            {
                ModelState.AddModelError( "Question.Option1","Please enter Option 1.");
            }

            if (isObjective && string.IsNullOrWhiteSpace( model.Question.Option2))
            {
                ModelState.AddModelError("Question.Option2","Please enter Option 2.");
            }

            if (isObjective && string.IsNullOrWhiteSpace(model.Question.Option3))
            {
                ModelState.AddModelError( "Question.Option3","Please enter Option 3.");
            }

            if (isObjective && string.IsNullOrWhiteSpace( model.Question.Option4))
            {
                ModelState.AddModelError( "Question.Option4", "Please enter Option 4.");
            }

            if (isObjective && string.IsNullOrWhiteSpace( model.Question.CorrectAnswer))
            {
                ModelState.AddModelError("Question.CorrectAnswer", "Please enter the correct answer.");
            }

            if (string.IsNullOrWhiteSpace( model.Question.QuestionType))
            {
                ModelState.AddModelError("Question.QuestionType","Please select Question Type.");
            }

            if (string.IsNullOrWhiteSpace( model.Question.DifficultyLevel))
            {
                ModelState.AddModelError( "Question.DifficultyLevel","Please select Difficulty.");
            }


            /* =====================================================
               RETURN PAGE WITH DATA
               ===================================================== */

            if (!ModelState.IsValid)
            {
                await _questionCreationService.PreparePostModelAsync(model, userId.Value);
                return View(model);
            }


            /* =====================================================
               DUPLICATE CHECK (offline Python similarity API)
               A question at or above the similarity limit is not saved.
               If the API cannot be reached the question is saved and the page says so.
               ===================================================== */
            var duplicate = await CheckQuestionDuplicateAsync(model.Question.QuestionText, model.Question.QuestionId);
            if (duplicate.Available && duplicate.IsDuplicate)
            {
                model.DuplicateMatches = duplicate.Matches;
                ModelState.AddModelError("", "This question is too similar to an existing question (" + duplicate.TopScore.ToString("0.00") + "%, limit " + _duplicateCheckService.ThresholdPercent.ToString("0.##") + "%). It was not saved. Change the question and try again.");
                await _questionCreationService.PreparePostModelAsync(model, userId.Value);
                return View(model);
            }
            model.DuplicateCheckUnavailable = !duplicate.Available;
            if (!duplicate.Available)
            {
                TempData["WarningMessage"] = "The duplicate check service could not be reached, so the last question was saved without a duplicate check.";
            }

            /* =====================================================
               SAVE
               ===================================================== */
            var saveResult = await _questionCreationService .SaveQuestionAsync( model.AssignmentId, userId.Value, model.Question);


            /* =====================================================
               QUOTA ERROR
               ===================================================== */

            if (!saveResult.Success)
            {
                ModelState.AddModelError( "", saveResult.ErrorMessage);
                model.Quota = saveResult.Quota ??await GetQuotaSafelyAsync( model.AssignmentId,userId.Value);
                await _questionCreationService .PreparePostModelAsync(model, userId.Value);
                return View(model);
            }
            long savedQuestionId = saveResult.QuestionId;


            /* =====================================================
               SAVE ONLY
               ===================================================== */

            if (string.Equals( navigationAction, "save", StringComparison.OrdinalIgnoreCase))
            {
                TempData["SuccessMessage"] = "Question saved successfully.";
                return RedirectToAction( nameof(CreateQuestion),
                    new
                    {
                        assignmentId =model.AssignmentId,
                        questionId =savedQuestionId,
                        newQuestion = false
                    });
            }


            /* =====================================================
               NEXT
               ===================================================== */

            if (string.Equals(navigationAction, "next", StringComparison.OrdinalIgnoreCase))
            {
                var nextQuestionId =await GetNextQuestionIdSafelyAsync( model.AssignmentId, userId.Value,savedQuestionId);

                if (nextQuestionId.HasValue)
                {
                    return RedirectToAction( nameof(CreateQuestion),
                        new
                        {
                            assignmentId = model.AssignmentId,
                            questionId =nextQuestionId.Value,
                            newQuestion = false
                        });
                }
                var quota =saveResult.Quota ??await GetQuotaSafelyAsync(model.AssignmentId, userId.Value);


                /* =============================================
                   MORE QUOTA AVAILABLE
                   ============================================= */

                if (quota != null &&quota.TotalRemaining > 0)
                {
                    return RedirectToAction(nameof(CreateQuestion),
                        new
                        {
                            assignmentId =
                                model.AssignmentId,

                            questionId =
                                (long?)null,

                            newQuestion = true
                        });
                }


                /* =============================================
                   QUOTA COMPLETED
                   ============================================= */

                TempData["SuccessMessage"] ="Question saved. All assigned question quota has been completed.";
                return RedirectToAction("Dashboard");
            }


            /* =====================================================
               DEFAULT
               ===================================================== */

            return RedirectToAction( nameof(CreateQuestion),
                new
                {
                    assignmentId =  model.AssignmentId,
                    questionId =savedQuestionId
                });
        }


        /* =========================================================
           HELPERS
           ========================================================= */

        private sealed class DuplicateOutcome
        {
            public bool Available;
            public bool IsDuplicate;
            public decimal TopScore;
            public List<DuplicateMatchModel> Matches = new List<DuplicateMatchModel>();
        }

        // Asks the Python API for the closest saved questions. The question itself is excluded
        // (a saved draft is already in the index and would match itself at 100%).
        private async Task<DuplicateOutcome> CheckQuestionDuplicateAsync(string questionHtml, long ownQuestionId)
        {
            var outcome = new DuplicateOutcome();
            try
            {
                var matches = await _duplicateCheckService.GetTopMatchesAsync(questionHtml ?? "");
                outcome.Available = true;
                outcome.Matches = matches.Where(m => m.QuestionId != ownQuestionId).ToList();
                outcome.TopScore = outcome.Matches.Count == 0 ? 0m : outcome.Matches.Max(m => m.SimilarityPercent);
                outcome.IsDuplicate = outcome.TopScore >= _duplicateCheckService.ThresholdPercent;
            }
            catch (Exception)
            {
                outcome.Available = false;
            }
            return outcome;
        }

        public class DuplicateCheckRequest
        {
            public string QuestionText { get; set; }
            public long QuestionId { get; set; }
        }

        // Called by the "Check duplicates" button (and automatically when the question box loses focus).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckDuplicate([FromBody] DuplicateCheckRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.QuestionText))
            {
                return Json(new { available = true, isDuplicate = false, threshold = _duplicateCheckService.ThresholdPercent, matches = new object[0] });
            }

            var outcome = await CheckQuestionDuplicateAsync(request.QuestionText, request.QuestionId);
            if (!outcome.Available)
            {
                return Json(new { available = false, message = "Duplicate check is not available right now. It will be tried again when you save." });
            }

            return Json(new
            {
                available = true,
                isDuplicate = outcome.IsDuplicate,
                threshold = _duplicateCheckService.ThresholdPercent,
                matches = outcome.Matches.Select(m => new { questionId = m.QuestionId, similarity = m.SimilarityPercent, text = m.QuestionText })
            });
        }

        private long? GetCurrentUserId()
        {
            var claim = User.FindFirst( ClaimTypes.NameIdentifier);

            if (claim == null)
            {
                return null;
            }

            long userId;

            if (!long.TryParse( claim.Value,out userId))
            {
                return null;
            }

            return userId;
        }


        private async Task<QuestionQuotaViewModel> GetQuotaSafelyAsync(long assignmentId, long userId)
        {
            var page =await _questionCreationService .GetCreatePageAsync(assignmentId,userId, null, true);

            return page == null ? new QuestionQuotaViewModel(): page.Quota;
        }


        private async Task<long?>GetNextQuestionIdSafelyAsync( long assignmentId, long userId, long questionId)
        {
            var page = await _questionCreationService .GetCreatePageAsync(assignmentId,userId,questionId,false);

            return page == null ? null : page.NextQuestionId;
        }






        //long setterUserId = GetCurrentUserId();

        //    if (model == null)
        //    {
        //        return BadRequest();
        //    }

        //    if (model.Question == null)
        //    {
        //        ModelState.AddModelError("","Question Information was not received.");
        //        return View(model);
        //    }
        //    if (model.Question.TopicId <= 0)
        //    {
        //        ModelState.AddModelError("Question.TopicId","Topic is required.");
        //    }

        //    if (string.IsNullOrWhiteSpace(model.Question.QuestionText))
        //    {
        //        ModelState.AddModelError("Question.QuestionText", "Question is required.");
        //    }

        //    if (string.IsNullOrWhiteSpace(model.Question.DifficultyLevel))
        //    {
        //        ModelState.AddModelError("Question.DifficultyLevel","Difficulty is required.");

        //    }

        //    if (!ModelState.IsValid)
        //    {
        //        return View(model);
        //    }
        //    var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        //    if (claim == null)
        //    {
        //        return RedirectToAction("Login", "Account");
        //    }
           
        //    if(!long.TryParse(claim.Value,out setterUserId))
        //    {
        //        return RedirectToAction("Login", "Account");
        //    }
        //    var quota =await _quotaService.GetQuotaAsync(model.AssignmentId,setterUserId);

        //    if (model.Question.DifficultyLevel == "EASY" && quota.EasyCreated >= quota.EasyRequired)
        //    {
        //        ModelState.AddModelError("", "Easy question quota is already completed.");
        //    }

        //    if (model.Question.DifficultyLevel == "MEDIUM" && quota.MediumCreated >= quota.MediumRequired)
        //    {
        //        ModelState.AddModelError( "","Medium question quota is already completed.");
        //    }

        //    if (model.Question.DifficultyLevel == "HARD" && quota.HardCreated >= quota.HardRequired)
        //    {
        //        ModelState.AddModelError( "", "Hard question quota is already completed.");
        //    }


        //    if (!ModelState.IsValid)
        //    {
        //        model.Quota = quota;
        //        return View(model);
        //    }

        //    var questionId = await _setterService.CreateQuestionAsync( model.Question, setterUserId);
        //    TempData["SuccessMessage"] = "Question created successfully. Question ID: "  + questionId;
        //    return RedirectToAction( nameof(Dashboard));
        //}


        [HttpPost]
        public async Task<IActionResult> SubmitToVetter(long questionId)
        {
            long setterUserId = (long)GetCurrentUserId();
            var result =   await _workflowService.SubmitToVetterAsync( questionId, setterUserId);
            if (!result)
            {
                TempData["ErrorMessage"] =
                    "Question cannot be submitted.";
            }
            else
            {
                TempData["SuccessMessage"] =
                    "Question submitted to Vetter.";
            }
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpGet]
        public async Task<IActionResult> EditQuestion(long questionId)
        {
            long setterUserId = (long)GetCurrentUserId();
            var model =await _setterService .GetQuestionForEditAsync(questionId,setterUserId);
            if (model == null)
            {
                return NotFound();
            }
            model.Difficulties =
                new List<string>
                {
            "EASY",
            "MEDIUM",
            "HARD"
                };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditQuestion( EditQuestionViewModel model)
        {
            long setterUserId = (long)GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(model.QuestionText))
            {
                ModelState.AddModelError("QuestionText","Question is required.");
            }

            if (string.IsNullOrWhiteSpace( model.DifficultyLevel))
            {
                ModelState.AddModelError( "DifficultyLevel", "Difficulty is required.");
            }

            if (!ModelState.IsValid)
            {
                model.Difficulties =
                    new List<string>
                    {
                "EASY",
                "MEDIUM",
                "HARD"
                    };

                return View(model);
            }

            var result = await _setterService .UpdateQuestionAsync( model, setterUserId);

            if (!result)
            {
                ModelState.AddModelError( "","Question cannot be edited in its current state.");
                return View(model);
            }

            TempData["SuccessMessage"] = "Question corrected successfully. It is now in DRAFT status.";
            return RedirectToAction(nameof(Dashboard));
        }

    }
}
