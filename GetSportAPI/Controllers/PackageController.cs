using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using GetSportAPI.Models.Generated;
using GetSportAPI.Models.Enum;
using GetSportAPI.DTO;
using Net.payOS;
using Net.payOS.Types;
using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PackageController : ControllerBase
    {
        private readonly GetSportContext _context;

        public PackageController(GetSportContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin}")]
        public async Task<ActionResult> Create([FromBody] PackageCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = new Dictionary<string, string[]>();
                foreach (var state in ModelState)
                {
                    if (state.Value.Errors.Any())
                    {
                        errors[state.Key] = state.Value.Errors.Select(e => e.ErrorMessage).ToArray();
                    }
                }
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid input data.", Errors = errors });
            }

            try
            {
                var package = new Package
                {
                    Name = dto.Name.Trim(),
                    Description = dto.Description?.Trim(),
                    Price = dto.Price,
                    Durationdays = dto.Durationdays,
                    Isactive = true,
                    Createat = DateTime.UtcNow
                };

                _context.Packages.Add(package);
                await _context.SaveChangesAsync();

                var responseData = new PackageResponseDto
                {
                    PackageId = package.PackageId,
                    Name = package.Name,
                    Description = package.Description,
                    Price = package.Price,
                    Durationdays = package.Durationdays,
                    Isactive = package.Isactive,
                    Createat = package.Createat,
                    Updateat = package.Updateat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Package created successfully.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while creating the package." });
            }
        }

        [HttpGet]
        public async Task<ActionResult> GetAll([FromQuery] bool? isActive = null)
        {
            var query = _context.Packages.AsQueryable();

            if (isActive.HasValue)
            {
                query = query.Where(p => p.Isactive == isActive.Value);
            }

            var packages = await query.ToListAsync();

            var responseData = packages.Select(p => new PackageResponseDto
            {
                PackageId = p.PackageId,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                Durationdays = p.Durationdays,
                Isactive = p.Isactive,
                Createat = p.Createat,
                Updateat = p.Updateat
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Packages retrieved successfully.", Data = responseData });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult> GetById(int id)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.PackageId == id);

            if (package == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Package not found." });
            }

            var responseData = new PackageResponseDto
            {
                PackageId = package.PackageId,
                Name = package.Name,
                Description = package.Description,
                Price = package.Price,
                Durationdays = package.Durationdays,
                Isactive = package.Isactive,
                Createat = package.Createat,
                Updateat = package.Updateat
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Package retrieved successfully.", Data = responseData });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = $"{UserRole.Admin}")]
        public async Task<ActionResult> Update(int id, [FromBody] PackageUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = new Dictionary<string, string[]>();
                foreach (var state in ModelState)
                {
                    if (state.Value.Errors.Any())
                    {
                        errors[state.Key] = state.Value.Errors.Select(e => e.ErrorMessage).ToArray();
                    }
                }
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid input data.", Errors = errors });
            }

            var package = await _context.Packages.FirstOrDefaultAsync(p => p.PackageId == id);

            if (package == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Package not found." });
            }

            try
            {
                if (!string.IsNullOrEmpty(dto.Name)) package.Name = dto.Name.Trim();
                if (dto.Description != null) package.Description = dto.Description.Trim();
                if (dto.Price.HasValue) package.Price = dto.Price.Value;
                if (dto.Durationdays.HasValue) package.Durationdays = dto.Durationdays.Value;
                if (dto.Isactive.HasValue) package.Isactive = dto.Isactive.Value;
                package.Updateat = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var responseData = new PackageResponseDto
                {
                    PackageId = package.PackageId,
                    Name = package.Name,
                    Description = package.Description,
                    Price = package.Price,
                    Durationdays = package.Durationdays,
                    Isactive = package.Isactive,
                    Createat = package.Createat,
                    Updateat = package.Updateat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Package updated successfully.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while updating the package." });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = $"{UserRole.Admin}")]
        public async Task<ActionResult> Delete(int id)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.PackageId == id);

            if (package == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Package not found." });
            }

            try
            {
                _context.Packages.Remove(package);
                await _context.SaveChangesAsync();

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Package deleted successfully." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while deleting the package." });
            }
        }
    }
}
