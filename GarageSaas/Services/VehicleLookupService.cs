using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SignupAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GarageSaas.Services
{
    public class VehicleLookupService : IVehicleLookupService
    {
        private readonly SignupContext _context;
        private readonly IMemoryCache _cache;

        public VehicleLookupService(SignupContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        // ---------- Makes ----------
        public async Task<List<SelectListItem>> GetVehicleMakesAsync()
        {
            return await _cache.GetOrCreateAsync("vehicle_makes", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);

                return await _context.VehicleMake
                    .AsNoTracking()
                    .Where(m => m.Active)
                    .OrderBy(m => m.Make)
                    .Select(m => new SelectListItem
                    {
                        Value = m.Id.ToString(),
                        Text = m.Make
                    })
                    .ToListAsync();
            });
        }

        // ---------- Models ----------
        public async Task<List<SelectListItem>> GetVehicleModelsAsync()
        {
            return await _cache.GetOrCreateAsync("vehicle_models_all", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);

                return await _context.VehicleModel
                    .AsNoTracking()
                    .Where(m => m.Active)
                    .OrderBy(m => m.Model)
                    .Select(m => new SelectListItem
                    {
                        Value = m.Id.ToString(),
                        Text = m.Model
                    })
                    .ToListAsync();
            });
        }

        public async Task<List<SelectListItem>> GetVehicleModelsByMakeAsync(int makeId)
        {
            var cacheKey = $"vehicle_models_make_{makeId}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);

                return await _context.VehicleModel
                    .AsNoTracking()
                    .Where(m => m.Active && m.MakeId == makeId)
                    .OrderBy(m => m.Model)
                    .Select(m => new SelectListItem
                    {
                        Value = m.Id.ToString(),
                        Text = m.Model
                    })
                    .ToListAsync();
            });
        }



        // ---------- Fuel ----------
        public async Task<List<SelectListItem>> GetFuelTypesAsync()
        {
            return await _cache.GetOrCreateAsync("fuel_types", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);

                return await _context.FuelType
                    .AsNoTracking()
                    .Where(f => f.Active)
                    .OrderBy(f => f.Fuel)
                    .Select(f => new SelectListItem
                    {
                        Value = f.Id.ToString(),
                        Text = f.Fuel
                    })
                    .ToListAsync();
            });
        }

        // ---------- Years ----------
        public async Task<List<SelectListItem>> GetVehicleYearsAsync()
        {
            return await _cache.GetOrCreateAsync("vehicle_years", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);

                return await _context.VehicleYears
                    .AsNoTracking()
                    .Where(y => y.Active)
                    .OrderByDescending(y => y.Years)
                    .Select(y => new SelectListItem
                    {
                        Value = y.Id.ToString(),
                        Text = y.Years
                    })
                    .ToListAsync();
            });
        }

        // ---------- Mileage ----------
        public async Task<List<SelectListItem>> GetMileageAsync()
        {
            return await _cache.GetOrCreateAsync("vehicle_mileage", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);

                return await _context.Mileage
                    .AsNoTracking()
                    .Where(m => m.Active)
                    .OrderBy(m => m.Kilometers)
                    .Select(m => new SelectListItem
                    {
                        Value = m.Id.ToString(),
                        Text = m.Kilometers
                    })
                    .ToListAsync();
            });
        }

        // ---------- Transmission Type ----------
        public async Task<List<SelectListItem>> GetTransmissionTypeAsync()
        {
            return await _cache.GetOrCreateAsync("vehicle_transmisson", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);

                return await _context.TransmissionType
                    .AsNoTracking()
                    .Where(m => m.Active)
                    .OrderBy(m => m.Transmission)
                    .Select(m => new SelectListItem
                    {
                        Value = m.Id.ToString(),
                        Text = m.Transmission
                    })
                    .ToListAsync();
            });
        }
    }
}
