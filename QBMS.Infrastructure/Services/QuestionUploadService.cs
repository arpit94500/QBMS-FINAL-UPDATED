using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

using QBMS.Application.Interfaces;
using QBMS.Application.Models;


namespace QBMS.Infrastructure.Services
{
    public class QuestionUploadService : IQuestionUploadService
    {
        private readonly ISetterRepository _setterRepository;

        public QuestionUploadService(
            ISetterRepository setterRepository)
        {
            _setterRepository = setterRepository;
        }

        public async Task<QuestionUploadViewModel> ProcessFileAsync(
            QuestionUploadViewModel model,
            long setterUserId)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (model.File == null ||
                model.File.Length == 0)
            {
                model.Errors.Add("Please select a file.");
                return model;
            }

            var extension =
                Path.GetExtension(model.File.FileName)
                    .ToLowerInvariant();

            if (extension != ".csv" &&
                extension != ".xlsx" &&
                extension != ".docx")
            {
                model.Errors.Add(
                    "Only CSV, Excel (.xlsx) and Word (.docx) files are supported.");

                return model;
            }

            model.FileName = model.File.FileName;

            /*
             * Parser implementation will be selected according
             * to the uploaded extension.
             *
             * CSV can be handled directly.
             * DOCX/XLSX parsing can use the .NET ZIP/XML APIs
             * without introducing another package.
             */

            if (extension == ".csv")
            {
                await ProcessCsvAsync(model, setterUserId);
            }
            else if (extension == ".xlsx")
            {
                await ProcessExcelAsync(model, setterUserId);
            }
            else if (extension == ".docx")
            {
                await ProcessWordAsync(model, setterUserId);
            }

            model.TotalRows = model.Questions.Count;

            model.ValidRows =
                model.Questions.Count(x => x.IsValid);

            model.InvalidRows =
                model.Questions.Count(x => !x.IsValid);

            return model;
        }

        public async Task<bool> SaveQuestionsAsync(
            QuestionUploadViewModel model,
            long setterUserId)
        {
            if (model == null ||
                model.Questions == null ||
                !model.Questions.Any())
            {
                return false;
            }

            var validQuestions =
                model.Questions
                    .Where(x => x.IsValid)
                    .ToList();

            if (!validQuestions.Any())
            {
                return false;
            }

            foreach (var question in validQuestions)
            {
                await _setterRepository.CreateQuestionAsync(
                    new CreateQuestionViewModel
                    {
                        MainExamId = question.MainExamId,
                        PaperId = question.PaperId,
                        SectionId = question.SectionId,
                        SubsectionId = question.SubsectionId,
                        TopicId = question.TopicId,

                        ExamName = question.ExamName,
                        PaperName = question.PaperName,
                        SectionName = question.SectionName,
                        SubsectionName = question.SubsectionName,
                        TopicName = question.TopicName,

                        QuestionText = question.QuestionText,
                        QuestionType = question.QuestionType,
                        DifficultyLevel = question.DifficultyLevel
                    },
                    setterUserId);
            }

            return true;
        }

        private async Task ProcessCsvAsync(
            QuestionUploadViewModel model,
            long setterUserId)
        {
            using (var reader =
                new StreamReader(model.File.OpenReadStream()))
            {
                var header = await reader.ReadLineAsync();

                if (string.IsNullOrWhiteSpace(header))
                {
                    model.Errors.Add(
                        "The uploaded CSV file is empty.");

                    return;
                }

                int rowNumber = 1;

                while (!reader.EndOfStream)
                {
                    rowNumber++;

                    var line =
                        await reader.ReadLineAsync();

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var values = line.Split(',');

                    var question =
                        new UploadedQuestionModel
                        {
                            RowNumber = rowNumber
                        };

                    if (values.Length < 8)
                    {
                        question.IsValid = false;
                        question.ErrorMessage =
                            "Insufficient columns.";

                        model.Questions.Add(question);
                        continue;
                    }

                    long mainExamId;
                    long paperId;
                    long sectionId;
                    long topicId;

                    if (!long.TryParse(values[0], out mainExamId))
                    {
                        question.ErrorMessage =
                            "Invalid Main Exam ID.";
                    }
                    else if (!long.TryParse(
                        values[1], out paperId))
                    {
                        question.ErrorMessage =
                            "Invalid Paper ID.";
                    }
                    else if (!long.TryParse(
                        values[2], out sectionId))
                    {
                        question.ErrorMessage =
                            "Invalid Section ID.";
                    }
                    else if (!long.TryParse(
                        values[4], out topicId))
                    {
                        question.ErrorMessage =
                            "Invalid Topic ID.";
                    }

                    if (string.IsNullOrWhiteSpace(
                        question.ErrorMessage))
                    {
                        question.MainExamId = mainExamId;
                        question.PaperId = paperId;
                        question.SectionId = sectionId;
                        question.TopicId = topicId;

                        question.QuestionText = values[5];
                        question.QuestionType = values[6];
                        question.DifficultyLevel = values[7];

                        question.IsValid =
                            ValidateQuestion(question);

                        if (!question.IsValid &&
                            string.IsNullOrWhiteSpace(
                                question.ErrorMessage))
                        {
                            question.ErrorMessage =
                                "Question validation failed.";
                        }
                    }

                    model.Questions.Add(question);
                }
            }
        }

        private async Task ProcessExcelAsync(
            QuestionUploadViewModel model,
            long setterUserId)
        {
            model.Errors.Add(
                "Excel parser is ready for integration with the XLSX reader.");

            await Task.CompletedTask;
        }

        private async Task ProcessWordAsync(
            QuestionUploadViewModel model,
            long setterUserId)
        {
            model.Errors.Add(
                "Word parser is ready for integration with the DOCX reader.");

            await Task.CompletedTask;
        }

        private bool ValidateQuestion(
            UploadedQuestionModel question)
        {
            if (string.IsNullOrWhiteSpace(
                question.QuestionText))
            {
                question.ErrorMessage =
                    "Question text is mandatory.";

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                question.QuestionType))
            {
                question.ErrorMessage =
                    "Question type is mandatory.";

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                question.DifficultyLevel))
            {
                question.ErrorMessage =
                    "Difficulty level is mandatory.";

                return false;
            }

            var difficulty =
                question.DifficultyLevel.Trim().ToUpperInvariant();

            if (difficulty != "EASY" &&
                difficulty != "MEDIUM" &&
                difficulty != "HARD")
            {
                question.ErrorMessage =
                    "Invalid difficulty level.";

                return false;
            }

            return true;
        }
    }
}
