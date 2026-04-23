using System;
using System.Collections.Generic;
using System.Linq;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using SignupAPI.Models;

namespace GarageSaas.Services
{
    public class VehiclePartService : IVehiclePartService
    {
        private readonly SignupContext _context;

        public VehiclePartService(SignupContext context)
        {
            _context = context;
        }

        public ServiceResult<VehiclePart> GetVehiclePart(int vehiclePartId)
        {
            var vehiclePart = _context.VehiclePart.FirstOrDefault(v => v.Id == vehiclePartId);

            if (vehiclePart == null)
            {
                return ServiceResult<VehiclePart>.Fail("Vehicle part not found.");
            }

            return ServiceResult<VehiclePart>.Ok(vehiclePart);
        }

        public ServiceResult<List<VehiclePart>> GetVehicleParts()
        {
            var vehicleParts = _context.VehiclePart
                .OrderBy(v => v.VehiclePartName)
                .ToList();

            return ServiceResult<List<VehiclePart>>.Ok(vehicleParts);
        }

        public ServiceResult<VehiclePart> AddOrUpdateVehiclePart(VehiclePart vehiclePart, string userName)
        {
            if (vehiclePart == null)
            {
                return ServiceResult<VehiclePart>.Fail("Vehicle part is null.");
            }

            if (string.IsNullOrWhiteSpace(vehiclePart.VehiclePartName))
            {
                return ServiceResult<VehiclePart>.Fail("Vehicle part name is required.");
            }

            if (vehiclePart.Id == 0)
            {
                var vehiclePartToAdd = new VehiclePart
                {
                    Part = vehiclePart.Part,
                    VehiclePartName = vehiclePart.VehiclePartName,
                    PartNumber = vehiclePart.PartNumber,
                    VehiclePartDescription = vehiclePart.VehiclePartDescription,
                    VehiclePartPrice = vehiclePart.VehiclePartPrice,
                    VehiclePartQuantity = vehiclePart.VehiclePartQuantity,
                    VehiclePartImage = vehiclePart.VehiclePartImage,
                    VehiclePartCategory = vehiclePart.VehiclePartCategory,
                    VehiclePartSubCategory = vehiclePart.VehiclePartSubCategory,
                    VehiclePartMake = vehiclePart.VehiclePartMake,
                    VehiclePartModel = vehiclePart.VehiclePartModel,
                    VehiclePartModelDetail = vehiclePart.VehiclePartModelDetail,
                    VehiclePartYear = vehiclePart.VehiclePartYear,
                    VehiclePartEngine = vehiclePart.VehiclePartEngine,
                    VehiclePartEngineSize = vehiclePart.VehiclePartEngineSize,
                    VehiclePartEngineModel = vehiclePart.VehiclePartEngineModel,
                    VehiclePartEngineFuelType = vehiclePart.VehiclePartEngineFuelType,
                    VehiclePartEngineCode = vehiclePart.VehiclePartEngineCode,
                    VehiclePartEngineBhp = vehiclePart.VehiclePartEngineBhp,
                    VehiclePartEngineKW = vehiclePart.VehiclePartEngineKW,
                    VehiclePartEngineCylinders = vehiclePart.VehiclePartEngineCylinders,
                    VehiclePartEngineValves = vehiclePart.VehiclePartEngineValves,
                    VehiclePartEngineManufactureDate = vehiclePart.VehiclePartEngineManufactureDate,
                    VehiclePartComment = vehiclePart.VehiclePartComment,
                    VehiclePartStatus = vehiclePart.VehiclePartStatus,
                    VehiclePartManufacturer = vehiclePart.VehiclePartManufacturer,
                    VehiclePartLocation = vehiclePart.VehiclePartLocation,
                    VehiclePartSupplier = vehiclePart.VehiclePartSupplier,
                    VehiclePartSupplierContact = vehiclePart.VehiclePartSupplierContact,
                    VehiclePartSupplierEmail = vehiclePart.VehiclePartSupplierEmail,
                    VehiclePartSupplierPhone = vehiclePart.VehiclePartSupplierPhone,
                    VehiclePartSupplierAddress = vehiclePart.VehiclePartSupplierAddress,
                    CreatedDate = DateTime.Now,
                    CreatedBy = userName
                };

                _context.VehiclePart.Add(vehiclePartToAdd);
                _context.SaveChanges();

                return ServiceResult<VehiclePart>.Ok(vehiclePartToAdd);
            }

            var vehiclePartToUpdate = _context.VehiclePart.FirstOrDefault(v => v.Id == vehiclePart.Id);

            if (vehiclePartToUpdate == null)
            {
                return ServiceResult<VehiclePart>.Fail("Vehicle part not found.");
            }

            vehiclePartToUpdate.Part = vehiclePart.Part;
            vehiclePartToUpdate.VehiclePartName = vehiclePart.VehiclePartName;
            vehiclePartToUpdate.PartNumber = vehiclePart.PartNumber;
            vehiclePartToUpdate.VehiclePartDescription = vehiclePart.VehiclePartDescription;
            vehiclePartToUpdate.VehiclePartPrice = vehiclePart.VehiclePartPrice;
            vehiclePartToUpdate.VehiclePartQuantity = vehiclePart.VehiclePartQuantity;
            vehiclePartToUpdate.VehiclePartImage = vehiclePart.VehiclePartImage;
            vehiclePartToUpdate.VehiclePartCategory = vehiclePart.VehiclePartCategory;
            vehiclePartToUpdate.VehiclePartSubCategory = vehiclePart.VehiclePartSubCategory;
            vehiclePartToUpdate.VehiclePartMake = vehiclePart.VehiclePartMake;
            vehiclePartToUpdate.VehiclePartModel = vehiclePart.VehiclePartModel;
            vehiclePartToUpdate.VehiclePartModelDetail = vehiclePart.VehiclePartModelDetail;
            vehiclePartToUpdate.VehiclePartYear = vehiclePart.VehiclePartYear;
            vehiclePartToUpdate.VehiclePartEngine = vehiclePart.VehiclePartEngine;
            vehiclePartToUpdate.VehiclePartEngineSize = vehiclePart.VehiclePartEngineSize;
            vehiclePartToUpdate.VehiclePartEngineModel = vehiclePart.VehiclePartEngineModel;
            vehiclePartToUpdate.VehiclePartEngineFuelType = vehiclePart.VehiclePartEngineFuelType;
            vehiclePartToUpdate.VehiclePartEngineCode = vehiclePart.VehiclePartEngineCode;
            vehiclePartToUpdate.VehiclePartEngineBhp = vehiclePart.VehiclePartEngineBhp;
            vehiclePartToUpdate.VehiclePartEngineKW = vehiclePart.VehiclePartEngineKW;
            vehiclePartToUpdate.VehiclePartEngineCylinders = vehiclePart.VehiclePartEngineCylinders;
            vehiclePartToUpdate.VehiclePartEngineValves = vehiclePart.VehiclePartEngineValves;
            vehiclePartToUpdate.VehiclePartEngineManufactureDate = vehiclePart.VehiclePartEngineManufactureDate;
            vehiclePartToUpdate.VehiclePartComment = vehiclePart.VehiclePartComment;
            vehiclePartToUpdate.VehiclePartStatus = vehiclePart.VehiclePartStatus;
            vehiclePartToUpdate.VehiclePartManufacturer = vehiclePart.VehiclePartManufacturer;
            vehiclePartToUpdate.VehiclePartLocation = vehiclePart.VehiclePartLocation;
            vehiclePartToUpdate.VehiclePartSupplier = vehiclePart.VehiclePartSupplier;
            vehiclePartToUpdate.VehiclePartSupplierContact = vehiclePart.VehiclePartSupplierContact;
            vehiclePartToUpdate.VehiclePartSupplierEmail = vehiclePart.VehiclePartSupplierEmail;
            vehiclePartToUpdate.VehiclePartSupplierPhone = vehiclePart.VehiclePartSupplierPhone;
            vehiclePartToUpdate.VehiclePartSupplierAddress = vehiclePart.VehiclePartSupplierAddress;
            vehiclePartToUpdate.UpdatedDate = DateTime.Now;
            vehiclePartToUpdate.UpdatedBy = userName;

            _context.SaveChanges();

            return ServiceResult<VehiclePart>.Ok(vehiclePartToUpdate);
        }

        public ServiceResult DeleteVehiclePart(int vehiclePartId)
        {
            var vehiclePart = _context.VehiclePart.FirstOrDefault(v => v.Id == vehiclePartId);

            if (vehiclePart == null)
            {
                return ServiceResult.Fail("Vehicle part not found.");
            }

            _context.VehiclePart.Remove(vehiclePart);
            _context.SaveChanges();

            return ServiceResult.Ok();
        }
    }
}