using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using QBMS.Application.Interfaces;
using System.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using QBMS.Application.Models;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace QBMS1.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class AdminController : Controller
    {
        private readonly ITosService _tosService;
        private readonly ITosHierarchyService _tosHierarchyService;
        private readonly IUserRepository _userRepository;
        private readonly IAdminQuestionService _adminQuestionService;
        private readonly IUserManagementService
    _userManagementService;
        private readonly ICurrentUserService _currentUser;
        private readonly SecuritySettings _securitySettings;


        private readonly IQuestionAssignmentService _assignmentService;
        private readonly IReportRepository _reportRepository;
        private readonly IActivityLogRepository _activityLogRepository;

        public AdminController(
    ITosService tosService,
    ITosHierarchyService tosHierarchyService,
    IUserRepository userRepository,
    IQuestionAssignmentService assignmentService,
    IAdminQuestionService adminQuestionService,
    IUserManagementService userManagementService, ICurrentUserService currentUser,
    SecuritySettings securitySettings,
    IReportRepository reportRepository,
    IActivityLogRepository activityLogRepository)
        {
            _activityLogRepository = activityLogRepository;
            _reportRepository = reportRepository;
            _tosService = tosService;

            _tosHierarchyService =
                tosHierarchyService;

            _userRepository =
                userRepository;

            _assignmentService =
                assignmentService;

            _adminQuestionService =
                adminQuestionService;

            _userManagementService =
                userManagementService;
            _currentUser = currentUser;
            _securitySettings = securitySettings;
        }

        private long GetCurrentUserId()
        {
            if (!_currentUser.IsAuthenticated ||
                _currentUser.UserId <= 0)
            {
                throw new System.InvalidOperationException(
                    "Logged-in Admin could not be identified.");
            }

            return _currentUser.UserId;
        }


        [HttpGet]
        public IActionResult SqlClientInfo()
        {
            var assembly = typeof(SqlConnection).Assembly;
            return Content("Assembly: "+assembly.FullName+ Environment.NewLine + "Location: "+ assembly.Location+Environment.NewLine+
                "Version: "+assembly.GetName().Version);
        }
        [HttpGet]
        public IActionResult Dashboard()
        {
            return View();
        }

        [HttpGet]
        public IActionResult TosSync()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SyncTos()
        {
            var count =
                await _tosService.SyncAsync();

            TempData["SuccessMessage"] =
                count + " TOS records synchronized successfully.";

            return RedirectToAction(
                nameof(TosSync));
        }

       
        [HttpGet]
        public async Task<IActionResult> TosManagement()
        {
            //var tosList =
            //    await _tosService.GetAllAsync();

            //return View(tosList);
            var model = new TosAssignmentViewModel();

            model.Exams =
                await _tosHierarchyService.GetExamsAsync();

            model.Papers =
                new List<DropdownItem>();

            model.Sections =
                new List<DropdownItem>();

            model.Subsections =
                new List<DropdownItem>();

            model.Topics =
                new List<DropdownItem>();
            model.Setters = await _userRepository.GetSettersAsync();
            model.Vetters = await _userRepository.GetVetterAsync();
            return View(model);

        }

        [HttpGet]
        public IActionResult AssignSetter()
        {
            return View();
        }

        [HttpGet]
        public IActionResult AssignVetter()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> UserManagement()
        {
            var users =
                await _userManagementService
                    .GetUsersAsync();

            return View(users);
        }
        [HttpGet]
        public async Task<IActionResult> TosAssignmentManagement(string search = "", long? examId = null, long? paperId = null)
        {
            var model = new TosAssignmentManagementViewModel
            {
                Search = search,
                ExamId = examId,
                PaperId = paperId,
                Exams = await _tosHierarchyService.GetExamsAsync(),
                Papers = examId.HasValue
                    ? await _tosHierarchyService.GetPapersAsync(examId.Value)
                    : new List<DropdownItem>(),
                Assignments = await _assignmentService.GetAllForManagementAsync(search, examId, paperId),
                Setters = await _userRepository.GetSettersAsync(),
                Vetters = await _userRepository.GetVetterAsync()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditTosAssignment(long id)
        {
            var model = await _assignmentService.GetForReassignmentAsync(id);

            if (model == null)
                return NotFound();

            model.Setters = await _userRepository.GetSettersAsync();
            model.Vetters = await _userRepository.GetVetterAsync();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTosAssignment(
            ReassignTosAssignmentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Setters = await _userRepository.GetSettersAsync();
                model.Vetters = await _userRepository.GetVetterAsync();
                return View(model);
            }

            try
            {
                await _assignmentService.ReassignAsync(
                    model.AssignmentId,
                    model.NewSetterUserId,
                    model.NewVetterUserId,
                    GetCurrentUserId(),
                    model.TransferUnfinishedQuestions);

                TempData["SuccessMessage"] =
                    "Assignment reassigned successfully.";

                return RedirectToAction(nameof(TosAssignmentManagement));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);

                model.Setters = await _userRepository.GetSettersAsync();
                model.Vetters = await _userRepository.GetVetterAsync();

                return View(model);
            }
        }



        [HttpGet]
        public async Task<IActionResult> CreateUser()
        {
            var model = new CreateUserViewModel { IsActive = true, Roles = await _userManagementService.GetRolesAsync() };
            ViewBag.DefaultPassword = _securitySettings.DefaultPassword;
            return View(
               model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(
    CreateUserViewModel model)
        {
            ViewBag.DefaultPassword = _securitySettings.DefaultPassword;

            if (!ModelState.IsValid)
            {
                model.Roles = await _userManagementService.GetRolesAsync();
                return View(model);
            }

            try
            {
                var userId =
                    await _userManagementService
                        .CreateUserAsync(model);

                TempData["SuccessMessage"] =
                    "User created successfully. User ID: "
                    + userId
                    + ". Default password: "
                    + _securitySettings.DefaultPassword
                    + " (the user must change it at first login).";

                return RedirectToAction(
                    nameof(UserManagement));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(
                    "",
                    ex.Message);
                model.Roles = await _userManagementService.GetRolesAsync();
                return View(model);
            }
        }

       
 
[HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserStatus(
    long userId,
    bool isActive)
        {
            var result =
                await _userManagementService
                    .UpdateStatusAsync(
                        userId,
                        isActive);

            if (result)
            {
                TempData["SuccessMessage"] =
                    isActive
                        ? "User activated successfully."
                        : "User deactivated successfully.";
            }
            else
            {
                TempData["ErrorMessage"] =
                    "Unable to update user status.";
            }

            return RedirectToAction(
                nameof(UserManagement));
        }


        [HttpGet]
        public async Task<IActionResult> GetEmployeeName(string personnelNo)
        {
            var result =
                await _userManagementService
                    .LookupEmployeeAsync(personnelNo);

            return Json(new
            {
                success = result.Success,
                name = result.Name,
                message = result.Message
            });
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(long id)
        {
            var model =
                await _userManagementService
                    .GetUserForEditAsync(id);

            if (model == null)
            {
                TempData["ErrorMessage"] = "User not found.";

                return RedirectToAction(
                    nameof(UserManagement));
            }

            model.Roles = await _userManagementService.GetRolesAsync();
            ViewBag.DefaultPassword = _securitySettings.DefaultPassword;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(
            EditUserViewModel model)
        {
            // Personnel No / name are display-only: always reload them from the database.
            var existing =
                await _userManagementService
                    .GetUserForEditAsync(model.UserId);

            if (existing == null)
            {
                TempData["ErrorMessage"] = "User not found.";

                return RedirectToAction(
                    nameof(UserManagement));
            }

            model.PersonnelNo = existing.PersonnelNo;
            model.UserName = existing.UserName;
            ViewBag.DefaultPassword = _securitySettings.DefaultPassword;

            if (!ModelState.IsValid)
            {
                model.Roles = await _userManagementService.GetRolesAsync();
                return View(model);
            }

            try
            {
                var updated =
                    await _userManagementService
                        .UpdateUserAsync(model);

                if (!updated)
                {
                    throw new InvalidOperationException(
                        "Unable to update the user.");
                }

                TempData["SuccessMessage"] =
                    "User updated successfully.";

                return RedirectToAction(
                    nameof(UserManagement));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                model.Roles = await _userManagementService.GetRolesAsync();
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetUserPassword(long userId)
        {
            var result =
                await _userManagementService
                    .ResetPasswordAsync(userId);

            if (result)
            {
                TempData["SuccessMessage"] =
                    "Password reset to the default password ("
                    + _securitySettings.DefaultPassword
                    + "). The user must change it at next login.";
            }
            else
            {
                TempData["ErrorMessage"] =
                    "Unable to reset the password.";
            }

            return RedirectToAction(
                nameof(EditUser),
                new { id = userId });
        }

        [HttpGet]
        public IActionResult QuestionReview()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> FinalApproval(long? examId, long? paperId)
        {
            var questions =
                await _adminQuestionService
                    .GetPendingQuestionsAsync(examId,paperId);

            ViewBag.Exams = await _tosHierarchyService.GetExamsAsync();
            ViewBag.Papers = examId.HasValue ? await _tosHierarchyService.GetPapersAsync(examId.Value) : new List<DropdownItem>();
            ViewBag.SelectedExamId = examId;
            ViewBag.SelectedPaperId = paperId;
            return View(questions);
        }
        [HttpPost]
        public async Task<IActionResult> ReviewQuestion(
            long questionId,
            string decision,
            string remarks)
        {
            // Temporary Admin ID.
            long adminUserId =
    GetCurrentUserId();


            var result =
                await _adminQuestionService
                    .ReviewQuestionAsync(
                        questionId,
                        adminUserId,
                        decision,
                        remarks);

            if (!result)
            {
                TempData["ErrorMessage"] =
                    "Question could not be reviewed.";
            }
            else if (decision == "APPROVE")
            {
                TempData["SuccessMessage"] =
                    "Question approved successfully.";
            }
            else
            {
                TempData["SuccessMessage"] =
                    "Question rejected and returned to Setter.";
            }

            return RedirectToAction(
                nameof(FinalApproval));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkReviewQuestions(List<long> questionIds,string decision,string remarks)
        {
            if (questionIds == null || questionIds.Count == 0)
            {
                TempData["ErrorMessage"] = "Please select at least one question";
                return RedirectToAction(nameof(FinalApproval));
            }
                long adminUserId = GetCurrentUserId();
                int successCount= 0;
                foreach(var questionsId in questionIds)
                {
                    var result = await _adminQuestionService.ReviewQuestionAsync(questionsId, adminUserId, decision, remarks);
                    if (result)
                    {
                        successCount++;
                    }
                }
                if (decision =="APPROVE")
                {
                    TempData["SuccessMessage"] = successCount + "questions(s) approved successfully.";
                }
                else if (decision=="REJECT")
                {
                    TempData["SuccessMessage"] = successCount + "question(s) rejected successfully.";
                }
                return RedirectToAction(nameof(FinalApproval));

            
           
        }


        [HttpGet]
        public async Task<IActionResult> ActivityLogs(string personnelNo = null, string role = null, string search = null, DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 25)
        {
            var model = await _activityLogRepository.GetPageAsync(new ActivityLogQuery
            {
                PersonnelNo = personnelNo,
                Role = role,
                Search = search,
                From = from,
                To = to,
                Page = page,
                PageSize = pageSize
            });

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Reports(long? examId = null)
        {
            var rows = await _reportRepository.GetAssignmentProgressAsync(examId);

            ProgressSummary Add(ProgressSummary s, AssignmentProgressRow r)
            {
                s.Assignments++;
                s.Target += r.Target;
                s.Created += r.Created;
                s.Draft += r.Draft;
                s.Submitted += r.Submitted;
                s.VetterApproved += r.VetterApproved;
                s.Rejected += r.Rejected;
                s.Completed += r.Completed;
                return s;
            }

            var model = new ReportsViewModel
            {
                ExamId = examId,
                Exams = await _tosHierarchyService.GetExamsAsync()
            };

            foreach (var r in rows)
            {
                Add(model.Total, r);

                var setter = model.Setters.FirstOrDefault(x => x.Key == r.SetterUserId);
                if (setter == null) { setter = new ProgressSummary { Key = r.SetterUserId, Name = r.SetterName }; model.Setters.Add(setter); }
                Add(setter, r);

                if (r.VetterUserId.HasValue)
                {
                    var vetter = model.Vetters.FirstOrDefault(x => x.Key == r.VetterUserId.Value);
                    if (vetter == null) { vetter = new ProgressSummary { Key = r.VetterUserId.Value, Name = r.VetterName ?? "(unknown)" }; model.Vetters.Add(vetter); }
                    Add(vetter, r);
                }

            }

            // Paper-wise progress (one line per paper; an assignment counts for every paper it covers)
            var paperRows = await _reportRepository.GetPaperProgressAsync(examId);
            foreach (var r in paperRows)
            {
                var paper = model.PaperWise.FirstOrDefault(x => x.Key == r.PaperId);
                if (paper == null)
                {
                    paper = new ProgressSummary
                    {
                        Key = r.PaperId,
                        Name = string.IsNullOrWhiteSpace(r.ExamName) ? r.PaperName : r.ExamName + " - " + r.PaperName
                    };
                    model.PaperWise.Add(paper);
                }
                Add(paper, r);
            }

            model.Setters = model.Setters.OrderBy(x => x.Name).ToList();
            model.Vetters = model.Vetters.OrderBy(x => x.Name).ToList();
            model.PaperWise = model.PaperWise.OrderBy(x => x.Name).ToList();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetPapers(
    long examId)
        {
            var result =
                await _tosHierarchyService
                    .GetPapersAsync(examId);

            return Json(result);
        }
        [HttpGet]
        public async Task<IActionResult> GetSections(
            long paperId)
        {
            var result =
                await _tosHierarchyService
                    .GetSectionsAsync(paperId);

            return Json(result);
        }
        [HttpGet]
        public async Task<IActionResult> GetSubsections(
            long sectionId)
        {
            var result =
                await _tosHierarchyService
                    .GetSubsectionsAsync(sectionId);

            return Json(result);
        }
        [HttpGet]
        public async Task<IActionResult> GetTopics(
            long subsectionId)
        {
            var result =
                await _tosHierarchyService
                    .GetTopicsAsync(subsectionId);

            return Json(result);
        }
        private bool ValidateAssignmentHierarchy(TosAssignmentRequest model)
        {
            if (model.ExamId <= 0)
            {
                return false;
            }
            switch (model.AssignmentLevel)
            {
                // "EXAM" is no longer an assignment level (existing EXAM assignments keep working).
                case "PAPER":
                    return model.PaperId.HasValue && model.PaperId.Value > 0;
                case "SECTION":
                    return model.PaperId.HasValue && model.PaperId.Value > 0 && model.SectionId.HasValue && model.SectionId.Value>0;
                case "SUBSECTION":
                    return model.PaperId.HasValue && model.PaperId.Value > 0 && model.SectionId.HasValue && model.SectionId.Value > 0
                        && model.SubsectionId.HasValue && model.SubsectionId.Value > 0;
                case "TOPIC":
                    return model.PaperId.HasValue && model.PaperId.Value > 0 && model.SectionId.HasValue && model.SectionId.Value > 0
                        && model.SubsectionId.HasValue && model.SubsectionId.Value > 0 && model.TopicId.HasValue && model.TopicId.Value>0;
                default:
                    return false;
            }
        }
        [HttpPost]
        public async Task<IActionResult> CreateAssignment([FromBody] TosAssignmentRequest request)
        {
            try
            {
                long adminUserId = GetCurrentUserId();
                if (request == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Assignment request is empty."
                    }); 
                }
                if (!ValidateAssignmentHierarchy(request))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Please select the complete hierarchy for the selected assignment level."
                    });
                }
                var assignmentId = await _assignmentService.CreateAsync(request, adminUserId);
                return Json(new {
                    success=true,
                    assignmentId=assignmentId,
                    message="Assignment Created Successfully."
                });
            }
            catch(Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }
    }

}
