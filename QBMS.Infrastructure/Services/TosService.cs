//using System.Collections.Generic;
//using System.Threading.Tasks;
//using QBMS.Application.Interfaces;
//using QBMS.Application.Models;

//namespace QBMS.Infrastructure.Services
//{
//    public class TosService : ITosService
//    {
//        private readonly ITosSyncRepository _repository;

//        public TosService(
//            ITosSyncRepository repository)
//        {
//            _repository = repository;
//        }

//        public async Task<List<TosViewModel>> GetAllAsync()
//        {
//            var rows =
//                await _repository.GetFromInoesAsync();

//            var result =
//                new List<TosViewModel>();

//            foreach (var item in rows)
//            {
//                result.Add(new TosViewModel
//                {
//                    ExamId = item.ExamId,
//                    ExamName = item.ExamName,

//                    PaperId = item.PaperId,
//                    PaperName = item.PaperName,

//                    SectionId = item.SectionId,
//                    SectionName = item.SectionName,

//                    SubsectionId = item.SubsectionId,
//                    SubsectionName = item.SubsectionName,

//                    TopicId = item.TopicId,
//                    TopicName = item.TopicName,

//                    TopicEasyQuestion = item.TopicEasyQuestion,
//                    TopicEasyMarks = item.TopicEasyMarks,

//                    TopicMediumQuestion = item.TopicMediumQuestion,
//                    TopicMediumMarks = item.TopicMediumMarks,

//                    TopicHardQuestion = item.TopicHardQuestion,
//                    TopicHardMarks = item.TopicHardMarks,

//                    IsActive = item.IsActive,
//                    TosStatus = item.TosStatus
//                });
//            }

//            return result;
//        }

//        public async Task<int> SyncAsync()
//        {
//            var rows =
//                await _repository.GetFromInoesAsync();

//            return await _repository.SyncToQbmsAsync(
//                rows);
//        }
//    }
//}

using System.Collections.Generic;
using System.Threading.Tasks;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class TosService : ITosService
    {
        private readonly ITosSyncRepository _syncRepository;
        private readonly ITosRepository _tosRepository;

        public TosService(
            ITosSyncRepository syncRepository,
            ITosRepository tosRepository)
        {
            _syncRepository = syncRepository;
            _tosRepository = tosRepository;
        }

        public async Task<List<TosViewModel>> GetAllAsync()
        {
            return await _tosRepository.GetAllAsync();
        }

        public async Task<int> SyncAsync()
        {
            var rows =
                await _syncRepository.GetFromInoesAsync();

            return await _syncRepository.SyncToQbmsAsync(
                rows);
        }
    }
}