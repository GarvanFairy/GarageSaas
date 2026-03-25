using System;

namespace SignupAPI.Models
{
    //Create class for vehicle part

    
       

    public class VehiclePart
    {
        //Create constructor for vehicle part
        public VehiclePart()
        {

        }
        //Create properties for vehicle part
        public int Id { get; set; }
        public string Part { get; set; }
        public string VehiclePartName { get; set; }

        public string PartNumber { get; set; }
        public string VehiclePartDescription { get; set; }
        public string VehiclePartPrice { get; set; }
        public string VehiclePartQuantity { get; set; }

        public string VehiclePartImage { get; set; }
        public string VehiclePartCategory { get; set; }
        public string VehiclePartSubCategory { get; set; }
        public string VehiclePartMake { get; set; }
        public string VehiclePartModel { get; set; }
        public string VehiclePartModelDetail { get; set; }
        public string VehiclePartYear { get; set; }
        public string VehiclePartEngine { get; set; }
        public string VehiclePartEngineSize { get; set; }
        public string VehiclePartEngineModel { get; set; }
        public string VehiclePartEngineFuelType { get; set; }
        public string VehiclePartEngineCode { get; set; }
        public string VehiclePartEngineBhp { get; set; }
        public string VehiclePartEngineKW { get; set; }
        public string VehiclePartEngineCylinders { get; set; }
        public string VehiclePartEngineValves { get; set; }
        public string VehiclePartEngineManufactureDate { get; set; }
        public string VehiclePartComment { get; set; }
        public string VehiclePartStatus { get; set; }
        public string VehiclePartManufacturer { get; set; }
        public string VehiclePartLocation { get; set; }
        public string VehiclePartSupplier { get; set; }
        public string VehiclePartSupplierContact { get; set; }
        public string VehiclePartSupplierEmail { get; set; }
        public string VehiclePartSupplierPhone { get; set; }
        public string VehiclePartSupplierAddress { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string UpdatedBy { get; set;}

    }
}
