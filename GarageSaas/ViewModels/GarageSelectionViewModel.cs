using System.Collections.Generic;
namespace GarageSaas.ViewModels
{
    public class GarageSelectionViewModel
    { public List<GarageSelectionItem> Garages { get; set; } = new List<GarageSelectionItem>(); }
    public class GarageSelectionItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public bool IsOwner { get; set; }
    }
}
