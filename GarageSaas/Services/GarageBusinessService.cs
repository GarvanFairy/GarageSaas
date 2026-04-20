using System;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using SignupAPI.Models;


namespace GarageSaas.Services
{
    public class GarageBusinessService : IGarageBusinessService
    {
        private readonly SignupContext _context;

        public GarageBusinessService(SignupContext context)
        {
            _context = context;
        }

        public ServiceResult<GarageBusiness> GetGarageBusinessDetail(int? garageBusinessId, int? userId)
        {
            if (garageBusinessId == null)
            {
                return ServiceResult<GarageBusiness>.Fail("Garage business id is required.");
            }

            if (userId == null)
            {
                return ServiceResult<GarageBusiness>.Fail("User id is required.");
            }

            var currentUser = _context.Users.Find(userId);
            if (currentUser == null)
            {
                return ServiceResult<GarageBusiness>.Fail("User not found.");
            }

            if (currentUser.GarageBusinessId != garageBusinessId)
            {
                return ServiceResult<GarageBusiness>.Fail("User is not authorised to view this garage business.");
            }

            var garage = _context.GarageBusiness.Find(garageBusinessId);
            if (garage == null)
            {
                return ServiceResult<GarageBusiness>.Fail("Garage business not found.");
            }

            return ServiceResult<GarageBusiness>.Ok(garage);
        }

        public ServiceResult<GarageBusiness> GetGarageBusinessForEdit(int? garageBusinessId, int sessionGarageBusinessId, int sessionUserId)
        {
            if (garageBusinessId == null)
            {
                return ServiceResult<GarageBusiness>.Fail("Garage business id is required.");
            }

            if (garageBusinessId != sessionGarageBusinessId)
            {
                return ServiceResult<GarageBusiness>.Fail("Session garage business id does not match.");
            }

            var currentUser = _context.Users.Find(sessionUserId);
            if (currentUser == null)
            {
                return ServiceResult<GarageBusiness>.Fail("User not found.");
            }

            if (currentUser.GarageBusinessId != garageBusinessId)
            {
                return ServiceResult<GarageBusiness>.Fail("User is not authorised to edit this garage business.");
            }

            var garage = _context.GarageBusiness.Find(garageBusinessId);
            if (garage == null)
            {
                return ServiceResult<GarageBusiness>.Fail("Garage business not found.");
            }

            return ServiceResult<GarageBusiness>.Ok(garage);
        }

        public ServiceResult<GarageBusiness> UpdateGarageBusiness(GarageBusiness garageBusiness, int sessionGarageBusinessId, int sessionUserId, string userName)
        {
            if (garageBusiness == null)
            {
                return ServiceResult<GarageBusiness>.Fail("Garage business is null.");
            }

            if (garageBusiness.Id != sessionGarageBusinessId)
            {
                return ServiceResult<GarageBusiness>.Fail("Garage business id does not match session.");
            }

            var currentUser = _context.Users.Find(sessionUserId);
            if (currentUser == null)
            {
                return ServiceResult<GarageBusiness>.Fail("User not found.");
            }

            if (currentUser.GarageBusinessId != garageBusiness.Id)
            {
                return ServiceResult<GarageBusiness>.Fail("User is not authorised to update this garage business.");
            }

            var garageToUpdate = _context.GarageBusiness.Find(garageBusiness.Id);
            if (garageToUpdate == null)
            {
                return ServiceResult<GarageBusiness>.Fail("Garage business not found.");
            }

            garageToUpdate.GarageBusinessName = garageBusiness.GarageBusinessName;
            garageToUpdate.GarageAddressLine1 = garageBusiness.GarageAddressLine1;
            garageToUpdate.GarageAddressLine2 = garageBusiness.GarageAddressLine2;
            garageToUpdate.GarageAddressLine3 = garageBusiness.GarageAddressLine3;
            garageToUpdate.GarageAddressLine4 = garageBusiness.GarageAddressLine4;
            garageToUpdate.Postcode = garageBusiness.Postcode;
            garageToUpdate.GarageEmailAddress = garageBusiness.GarageEmailAddress;
            garageToUpdate.GaragePhoneNumber = garageBusiness.GaragePhoneNumber;
            garageToUpdate.GarageMobileNumber = garageBusiness.GarageMobileNumber;
            garageToUpdate.UpdatedDate = DateTime.Now;
            garageToUpdate.UpdatedBy = userName;

            _context.SaveChanges();

            return ServiceResult<GarageBusiness>.Ok(garageToUpdate);
        }
    }
}