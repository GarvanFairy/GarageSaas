using System.ComponentModel.DataAnnotations;

namespace GarageSaas.ViewModels
{
    public class PlatformGarageStatusViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool Activate { get; set; }
        [Required] public bool? ExpectedActive { get; set; }
        [Required] public bool? ExpectedBlocked { get; set; }
        [Required] public string ExpectedUpdatedDate { get; set; }
        public string Error { get; set; }
    }
}
