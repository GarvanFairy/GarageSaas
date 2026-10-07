using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using SignupAPI.Models;
using System;
using System.Linq;


namespace GarageSaas.Services
{
    public class GarageBusinessService : IGarageBusinessService
    {
        private readonly SignupContext _context;

        public GarageBusinessService(SignupContext context)
        {
            _context = context;
        }

        public ServiceResult<GarageBusiness> GetGarageBusinessDetail(
            int garageBusinessId,
            int userId)
        {
            var currentUser =
                _context.Users
                    .FirstOrDefault(x =>
                        x.Id == userId &&
                        x.GarageBusinessId == garageBusinessId);

            if (currentUser == null)
            {
                return ServiceResult<GarageBusiness>
                    .Fail(
                        "User is not authorised to view this garage business.");
            }

            var garage =
                _context.GarageBusiness
                    .FirstOrDefault(x =>
                        x.Id == garageBusinessId);

            if (garage == null)
            {
                return ServiceResult<GarageBusiness>
                    .Fail("Garage business not found.");
            }

            return ServiceResult<GarageBusiness>
                .Ok(garage);
        }

        public ServiceResult<GarageBusiness>
            GetGarageBusinessForEdit(
                int garageBusinessId,
                int userId)
        {
            var currentUser =
                _context.Users.Find(userId);

            if (currentUser == null)
            {
                return ServiceResult<GarageBusiness>
                    .Fail("User not found.");
            }

            var membership =
                _context.GarageBusinessUser
                    .FirstOrDefault(x =>
                        x.UserId == userId &&
                        x.GarageBusinessId ==
                            garageBusinessId &&
                        x.IsActive);

            if (membership == null)
            {
                return ServiceResult<GarageBusiness>
                    .Fail(
                        "User is not authorised to edit this garage business.");
            }

            var garage =
                _context.GarageBusiness
                    .FirstOrDefault(x =>
                        x.Id == garageBusinessId &&
                        x.Active &&
                        !x.Blocked);

            if (garage == null)
            {
                return ServiceResult<GarageBusiness>
                    .Fail("Garage business not found.");
            }

            return ServiceResult<GarageBusiness>
                .Ok(garage);
        }

        public ServiceResult<GarageBusiness> UpdateGarageBusiness(
            GarageBusiness garageBusiness,
            int garageBusinessId,
            int userId,
            string userName)
        {
            if (garageBusiness == null)
            {
                return ServiceResult<GarageBusiness>
                    .Fail("Garage business is null.");
            }

            //
            // Verify that the current GarageSaas user belongs
            // to the authenticated garage.
            //
            var currentUser =
                _context.Users
                    .FirstOrDefault(x =>
                        x.Id == userId &&
                        x.GarageBusinessId == garageBusinessId);

            if (currentUser == null)
            {
                return ServiceResult<GarageBusiness>
                    .Fail(
                        "User is not authorised to update this garage business.");
            }

            //
            // IMPORTANT:
            // Use the trusted garageBusinessId obtained from
            // CurrentGarageUserService.
            //
            // Do NOT use garageBusiness.Id from the posted form
            // to decide which database record to update.
            //
            var garageToUpdate =
                _context.GarageBusiness
                    .FirstOrDefault(x =>
                        x.Id == garageBusinessId);

            if (garageToUpdate == null)
            {
                return ServiceResult<GarageBusiness>
                    .Fail("Garage business not found.");
            }

            //
            // Copy only properties that the garage owner
            // is permitted to edit.
            //
            garageToUpdate.GarageBusinessName =
                garageBusiness.GarageBusinessName;

            garageToUpdate.GarageAddressLine1 =
                garageBusiness.GarageAddressLine1;

            garageToUpdate.GarageAddressLine2 =
                garageBusiness.GarageAddressLine2;

            garageToUpdate.GarageAddressLine3 =
                garageBusiness.GarageAddressLine3;

            garageToUpdate.GarageAddressLine4 =
                garageBusiness.GarageAddressLine4;

            garageToUpdate.Postcode =
                garageBusiness.Postcode;

            garageToUpdate.GarageEmailAddress =
                garageBusiness.GarageEmailAddress;

            garageToUpdate.GaragePhoneNumber =
                garageBusiness.GaragePhoneNumber;

            garageToUpdate.GarageMobileNumber =
                garageBusiness.GarageMobileNumber;

            garageToUpdate.VatNumber =
                garageBusiness.VatNumber;

            garageToUpdate.BusinessRegistrationNumber =
                garageBusiness.BusinessRegistrationNumber;

            //
            // Only replace the existing logo if a new
            // logo was uploaded.
            //
            if (!string.IsNullOrWhiteSpace(
                garageBusiness.LogoImage))
            {
                garageToUpdate.LogoImage =
                    garageBusiness.LogoImage;
            }

            garageToUpdate.UpdatedDate =
                DateTime.Now;

            garageToUpdate.UpdatedBy =
                userName;

            _context.SaveChanges();

            return ServiceResult<GarageBusiness>
                .Ok(garageToUpdate);
        }
    }
}