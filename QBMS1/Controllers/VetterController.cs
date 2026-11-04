using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QBMS.Application.Interfaces;
using System.Security.Claims;
using System;
using QBMS.Application.Models;
using QBMS.Infrastructure.Services;

namespace QBMS1.Controllers
{
    [Authorize(Roles = "VETTER")]
    public class VetterController : Controller
    {
        private readonly IVetterService
            _vetterService;

        private readonly IQuestionWorkflowService
            _workflowService;
        private readonly ICurrentUserService _currentUser;
        private readonly IVetterQuestionReviewRepository _repository;
        private readonly IVetterQuestionReviewService _reviewService;

        public VetterController(
    IVetterService vetterService,
    IQuestionWorkflowService workflowService,
    ICurrentUserService currentUser,IVetterQuestionReviewRepository repository, IVetterQuestionReviewService reviewService)
        {
            _vetterService = vetterService;
            _workflowService = workflowService;
            _currentUser = currentUser;
            _repository = repository;
            _reviewService = reviewService;
        }

        private long GetCurrentUserId()
        {
            if (!_currentUser.IsAuthenticated ||
                _currentUser.UserId <= 0)
            {
                throw new System.InvalidOperationException(
                    "Logged-in Vetter could not be identified.");
            }

            return _currentUser.UserId;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            // Temporary Vetter ID.
            long vetterUserId = GetCurrentUserId();
            ;

            var exams =
                await _vetterService
                    .GetPendingExamSummaryAsync(
                        vetterUserId);

            return View(exams);
        }

        [HttpPost]
        public async Task<IActionResult> Review(
            long questionId,
            string decision,
            string remarks)
        {
            // Temporary Vetter ID.
            long vetterUserId = GetCurrentUserId();
            ;

            var result =
                await _workflowService
                    .ReviewQuestionAsync(
                        questionId,
                        vetterUserId,
                        decision,
                        remarks);

            if (!result)
            {
                TempData["ErrorMessage"] =
                    "Question could not be reviewed.";
            }
            else
            {
                TempData["SuccessMessage"] =
                    "Question reviewed successfully.";
            }

            return RedirectToAction(
                nameof(Dashboard));
        }


        public async Task<IActionResult> ReviewQuestion(long? id, long? examId)
        {
            long vetterUserId =
                Convert.ToInt64(
                    User.FindFirst(
                        System.Security.Claims.ClaimTypes.NameIdentifier)
                    ?.Value);

            long? questionId = id;

            // Coming from the dashboard: open the first pending
            // question of the selected exam.
            if (!questionId.HasValue && examId.HasValue)
            {
                questionId =
                    await _repository.GetFirstQuestionIdByExamAsync(
                        vetterUserId,
                        examId.Value);

                if (!questionId.HasValue)
                {
                    TempData["InfoMessage"] =
                        "No questions are pending for review.";

                    return RedirectToAction("Dashboard");
                }
            }

            if (!questionId.HasValue)
            {
                questionId =
                    await _repository.GetResumeQuestionIdAsync(
                        vetterUserId);
            }

            if (!questionId.HasValue)
            {
                var first =
                    await _repository.GetFirstQuestionAsync(
                        vetterUserId);

                if (first == null)
                {
                    TempData["InfoMessage"] =
                        "No questions are pending for review.";

                    return RedirectToAction("Dashboard");
                }

                questionId = first.QuestionId;
            }

            var question =
                await _repository.GetQuestionAsync(
                    vetterUserId,
                    questionId.Value);

            if (question == null)
                return RedirectToAction("Dashboard");

            question.PreviousQuestionId =
                await _repository.GetPreviousQuestionIdAsync(
                    vetterUserId,
                    question.QuestionId);

            question.NextQuestionId =
                await _repository.GetNextQuestionIdAsync(
                    vetterUserId,
                    question.QuestionId);

            question.CanGoPrevious =
                question.PreviousQuestionId.HasValue;

            question.CanGoNext =
                question.NextQuestionId.HasValue;

            await _repository.SaveProgressAsync(
                vetterUserId,
                question.QuestionId);

            return View(question);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles ="VETTER")]
        public async Task<IActionResult> SaveQuestionReview(VetterQuestionReviewViewModel model,string action)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if(claim==null)
            {
                return RedirectToAction("Login", "Account");
            }
            long vetterUserId = Convert.ToInt64(claim.Value);
            if (string.IsNullOrWhiteSpace(model.QuestionText))
            {
                TempData["ErrorMessage"] = "Question text cannot be empty.";
                return RedirectToAction("ReviewQuestion", new { id=model.QuestionId});
            }
            if((action=="APPROVE" || action=="REJECT") && string.IsNullOrWhiteSpace(model.Remarks))
            {
                TempData["ErrorMessage"] = "Please enter remarks.";
                return RedirectToAction("ReviewQuestion",new { id=model.QuestionId});
            }
            bool success = await _reviewService.SaveQuestionReviewAsync(vetterUserId, model.QuestionId, model.QuestionText,
                model.Option1, model.Option2, model.Option3,
            model.Option4, model.CorrectAnswer, model.Remarks, action);
            if(!success)
            {
                TempData["ErrorMessage"] = "Unable to save the question.";
                return RedirectToAction("ReviewQuestion", new { id = model.QuestionId });
            }
            if (action == "SAVE")
            {
                await _reviewService.SaveProgressAsync(vetterUserId, model.QuestionId);
                TempData["SuccessMessage"] = "Question Updated Successfully.";
                return RedirectToAction("ReviewQuestion", new { id = model.QuestionId });
            }
            var nextQuestionId = await _reviewService.GetNextQuestionIdAsync(vetterUserId, model.QuestionId);
            if(nextQuestionId.HasValue)
            {
                await _reviewService.SaveProgressAsync(vetterUserId, nextQuestionId.Value);
                return RedirectToAction("ReviewQuestion",new { id=nextQuestionId.Value});
            }
            TempData["SuccessMessage"] = action == "APPROVE" ? "Question Approved. No more Questions Are Pending." : "Question Rejected. No more Questions Are Pending.";
            return RedirectToAction("Dashboard");
        }

   








    }

}
