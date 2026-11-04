using QBMS.Application.Interfaces;
using QBMS.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace QBMS.Infrastructure.Services
{
    public class QuestionAssignmentService: IQuestionAssignmentService
    {
        private readonly IQuestionAssignmentRepository _repository;
        public QuestionAssignmentService(IQuestionAssignmentRepository repository)
        {
            _repository = repository;
        }
        public async Task<long> CreateAsync(TosAssignmentRequest request, long assignedBy)
        {
            ValidateRequest(request);
            return await _repository.CreateAsync(request, assignedBy);
        }
        private void ValidateRequest(TosAssignmentRequest request)
        {
            if(!request.ExamId.HasValue)
            {
                throw new ArgumentException("Exam is Required");
            }
            if (string.IsNullOrWhiteSpace(request.AssignmentLevel))
            {
                throw new ArgumentException("Assignment Level is Required");
            }
            if(request.SetterUserId<=0)
            {
                throw new ArgumentException("Setter is Required");
            }
            if (request.VetterUserId <= 0)
            {
                throw new ArgumentException("Vetter is Required");
            }
            if (request.MultiplicationFactor <= 0)
            {
                throw new ArgumentException("Multiplication factor must be greater than zero.");
            }
            switch (request.AssignmentLevel.ToUpper())
            {
                case "EXAM":
                    throw new ArgumentException("Assignment at Exam level is not available. Select Paper, Section, SubSection or Topic.");
                case "PAPER":
                    if(!request.PaperId.HasValue || request.PaperId.Value<=0)
                    {
                        throw new ArgumentException("Paper is Required.");
                    }
                    break;
                case "SECTION":
                    if(!request.PaperId.HasValue || request.PaperId.Value <= 0)
                    {
                        throw new ArgumentException("Paper is Required.");
                    }
                    if(!request.SectionId.HasValue || request.SectionId.Value <= 0)
                    {
                        throw new ArgumentException("Section is Required.");
                    }
                    break;
                case "SUBSECTION":
                    if (!request.PaperId.HasValue || request.PaperId.Value <= 0)
                    {
                        throw new ArgumentException("Paper is Required.");
                    }
                    if (!request.SectionId.HasValue || request.SectionId.Value <= 0)
                    {
                        throw new ArgumentException("Section is Required.");
                    }
                    if (!request.SubsectionId.HasValue || request.SubsectionId.Value <= 0)
                    {
                        throw new ArgumentException("Sub Section is Required.");
                    }
                    break;
                case "TOPIC":
                    if (!request.PaperId.HasValue || request.PaperId.Value <= 0)
                    {
                        throw new ArgumentException("Paper is Required.");
                    }
                    if (!request.SectionId.HasValue || request.SectionId.Value <= 0)
                    {
                        throw new ArgumentException("Section is Required.");
                    }
                    if (!request.SubsectionId.HasValue || request.SubsectionId.Value <= 0)
                    {
                        throw new ArgumentException("Sub Section is Required.");
                    }
                    if (!request.TopicId.HasValue || request.TopicId.Value <= 0)
                    {
                        throw new ArgumentException("Topic is Required.");
                    }
                    break;
                default:
                    throw new ArgumentException("Invalid Assignment level.");

            }
        }



        public Task<List<TosAssignmentManagementRow>>
            GetAllForManagementAsync(string search, long? examId = null, long? paperId = null)
        {
            return _repository.GetAllForManagementAsync(search, examId, paperId);
        }

        public Task<ReassignTosAssignmentViewModel>
            GetForReassignmentAsync(long assignmentId)
        {
            if (assignmentId <= 0)
                throw new ArgumentException("Invalid assignment ID.");

            return _repository.GetForReassignmentAsync(assignmentId);
        }

        public async Task ReassignAsync(
            long assignmentId,
            long newSetterUserId,
            long newVetterUserId,
            long assignedBy,
            bool transferUnfinishedQuestions)
        {
            if (assignmentId <= 0)
                throw new ArgumentException("Invalid assignment ID.");

            if (newSetterUserId <= 0)
                throw new ArgumentException("Please select a Setter.");

            if (newVetterUserId <= 0)
                throw new ArgumentException("Please select a Vetter.");

            if (assignedBy <= 0)
                throw new ArgumentException("Unable to identify the Admin.");

            await _repository.ReassignAsync(
                assignmentId,
                newSetterUserId,
                newVetterUserId,
                assignedBy,
                transferUnfinishedQuestions);
        }

    }
}
