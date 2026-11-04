using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QBMS.Application.Models
{
    public class EditUserViewModel
    {
        public long UserId { get; set; }

        // Read-only on screen: always reloaded from the database.
        [Display(Name = "Personnel No")]
        public string PersonnelNo { get; set; }

        [Display(Name = "User Name")]
        public string UserName { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Please select a role")]
        [Display(Name = "Role")]
        public int RoleId { get; set; }

        public bool IsActive { get; set; }

        public List<DropdownItem> Roles { get; set; }
    }
}
