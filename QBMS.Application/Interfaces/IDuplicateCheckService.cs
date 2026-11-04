using System.Collections.Generic;
using System.Threading.Tasks;
using QBMS.Application.Models;

namespace QBMS.Application.Interfaces
{
    public interface IDuplicateCheckService
    {
        decimal ThresholdPercent { get; }

        // Returns the top 3 most similar existing questions (highest first).
        // Throws if the Python API cannot be reached.
        Task<List<DuplicateMatchModel>> GetTopMatchesAsync(string questionText);

        // Semantic duplicates between the questions of one uploaded file.
        // Result[i] = most similar OTHER rows of the same file for questionTexts[i] (FileRowNumber is set).
        Task<List<List<DuplicateMatchModel>>> GetInFileMatchesAsync(IList<string> questionTexts, IList<int> rowNumbers);

        // Sends questions that have no embedding yet to the Python API so that
        // later uploads can be compared with them. Returns how many were indexed.
        Task<int> IndexPendingQuestionsAsync();
    }
}
