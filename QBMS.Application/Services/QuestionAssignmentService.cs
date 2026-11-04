using System;

namespace QBMS.Application.Services
{
    public class QuestionAssignmentService
    {
        public int CalculateAssignedCount(int questionCount, decimal multiplicationFactor)
        {
            if (questionCount <= 0)
            {
                throw new ArgumentException("Question Count must be greater than zero");
            }
            if (multiplicationFactor <= 0)
            {
                throw new ArgumentException("Multiplication Factor must be greater than zero");
            }
            return (int)Math.Ceiling(questionCount * multiplicationFactor);
        }
    }
}
