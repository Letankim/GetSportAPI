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
using GetSportAPI.Params;
using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PackageController : ControllerBase
    {
        private readonly GetSportContext _context;
        private readonly string[] _validSortFields = { "Name", "Price", "Durationdays", "Createat", "Updateat" };

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
                    Name = dto.Name?.Trim(),
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
            catch (ValidationException ex)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while creating the package." });
            }
        }

        [HttpGet]
        public async Task<ActionResult> GetAll([FromQuery] PackageFilterParams filterParams)
        {
            // Validate query parameters
            if (!string.IsNullOrEmpty(filterParams.SortBy) && !_validSortFields.Contains(filterParams.SortBy, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = $"Invalid sort field. Allowed values are: {string.Join(", ", _validSortFields)}." });
            }

            if (!string.IsNullOrEmpty(filterParams.SortOrder) && filterParams.SortOrder.ToLower() != "asc" && filterParams.SortOrder.ToLower() != "desc")
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid sort order. Allowed values are: asc, desc." });
            }

            if (filterParams.Page < 1)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Page number must be greater than 0." });
            }

            if (filterParams.PageSize < 1 || filterParams.PageSize > 100)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Page size must be between 1 and 100." });
            }

            // Build query
            var query = _context.Packages.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(filterParams.Search))
            {
                query = query.Where(p => p.Name != null && p.Name.ToLower().Contains(filterParams.Search.ToLower()));
            }

            if (filterParams.MinPrice.HasValue)
            {
                query = query.Where(p => p.Price >= filterParams.MinPrice.Value);
            }

            if (filterParams.MaxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= filterParams.MaxPrice.Value);
            }

            if (filterParams.MinDurationDays.HasValue)
            {
                query = query.Where(p => p.Durationdays >= filterParams.MinDurationDays.Value);
            }

            if (filterParams.MaxDurationDays.HasValue)
            {
                query = query.Where(p => p.Durationdays <= filterParams.MaxDurationDays.Value);
            }

            if (filterParams.IsActive.HasValue)
            {
                query = query.Where(p => p.Isactive == filterParams.IsActive.Value);
            }

            if (filterParams.StartCreateDate.HasValue)
            {
                query = query.Where(p => p.Createat >= filterParams.StartCreateDate.Value);
            }

            if (filterParams.EndCreateDate.HasValue)
            {
                query = query.Where(p => p.Createat <= filterParams.EndCreateDate.Value);
            }

            if (filterParams.StartUpdateDate.HasValue)
            {
                query = query.Where(p => p.Updateat != null && p.Updateat >= filterParams.StartUpdateDate.Value);
            }

            if (filterParams.EndUpdateDate.HasValue)
            {
                query = query.Where(p => p.Updateat != null && p.Updateat <= filterParams.EndUpdateDate.Value);
            }

            // Get total count for pagination
            var totalCount = await query.CountAsync();

            // Apply sorting
            var sortBy = filterParams.SortBy?.ToLower() ?? "createat";
            var isDescending = filterParams.SortOrder?.ToLower() == "desc";

            query = sortBy switch
            {
                "name" => isDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
                "price" => isDescending ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
                "durationdays" => isDescending ? query.OrderByDescending(p => p.Durationdays) : query.OrderBy(p => p.Durationdays),
                "updateat" => isDescending ? query.OrderByDescending(p => p.Updateat) : query.OrderBy(p => p.Updateat),
                _ => isDescending ? query.OrderByDescending(p => p.Createat) : query.OrderBy(p => p.Createat)
            };

            // Apply pagination
            query = query
                .Skip((filterParams.Page - 1) * filterParams.PageSize)
                .Take(filterParams.PageSize);

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

            var paginationMetadata = new
            {
                TotalCount = totalCount,
                PageSize = filterParams.PageSize,
                CurrentPage = filterParams.Page,
                TotalPages = (int)Math.Ceiling((double)totalCount / filterParams.PageSize)
            };

            return Ok(new
            {
                StatusCode = 200,
                Status = "Success",
                Message = "Packages retrieved successfully.",
                Pagination = paginationMetadata,
                Data = responseData
            });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult> GetById(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid package ID." });
            }

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

            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid package ID." });
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
            catch (ValidationException ex)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = ex.Message });
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
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid package ID." });
            }

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