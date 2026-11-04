using System.Collections.Generic;

namespace QBMS.Application.Models
{
    public class TosAssignmentViewModel
    {
        public long? ExamId { get; set; }

        public long? PaperId { get; set; }

        public long? SectionId { get; set; }

        public long? SubsectionId { get; set; }

        public long? TopicId { get; set; }

        public string AssignmentLevel { get; set; }

        public long? SetterUserId { get; set; }

        public long? VetterUserId { get; set; }

        public decimal MultiplicationFactor { get; set; }

        public List<DropdownItem> Exams { get; set; }

        public List<DropdownItem> Papers { get; set; }

        public List<DropdownItem> Sections { get; set; }

        public List<DropdownItem> Subsections { get; set; }

        public List<DropdownItem> Topics { get; set; }

        public List<DropdownItem> Setters { get; set; }

        public List<DropdownItem> Vetters { get; set; }
    }

    public class DropdownItem
    {
        public long Id { get; set; }

        public string Name { get; set; }
    }
}