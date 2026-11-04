using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QBMS.Application.Models
{
    public class CreateUserViewModel
    {
        [Required]
        [Display(Name = "Personnel No")]
        public string PersonnelNo { get; set; }

        // Populated from the HRCDF API (display only). The server ignores the
        // posted value and fetches the name from HRCDF again when saving.
        [Display(Name = "User Name")]
        public string UserName { get; set; }

        [Range(1,int.MaxValue,ErrorMessage ="Please Select a role")]
        [Display(Name = "Role")]
        public int RoleId { get; set; }

        public bool IsActive { get; set; } = true;
        public List<DropdownItem> Roles { get; set;}
    }
}
