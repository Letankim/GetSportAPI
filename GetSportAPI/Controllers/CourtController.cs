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
using GetSportAPI.Utils;
using System.ComponentModel.DataAnnotations;
using GetSportAPI.Params;
using GetSportAPI.Helpers;

namespace GetSportAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CourtController : ControllerBase
    {
        private readonly GetSportContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly string[] _validStatuses = { CourtStatus.Pending, CourtStatus.Approved, CourtStatus.Rejected, CourtStatus.Deleted };
        private readonly string[] _validSortFields = { "Priority", "Priceperhour", "Location", "Startdate", "Enddate" };

        public CourtController(GetSportContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> Create([FromForm] CourtCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(e => e.Value.Errors.Any())
                    .ToDictionary(e => e.Key, e => e.Value.Errors.Select(x => x.ErrorMessage).ToArray());

                return BadRequest(new
                {
                    StatusCode = 400,
                    Status = "BadRequest",
                    Message = "Invalid input data.",
                    Errors = errors
                });
            }

            var userInfo = UserHelper.GetUserInfo(User);
            if (userInfo.UserId == 0)
            {
                return Unauthorized(new
                {
                    StatusCode = 401,
                    Status = "Unauthorized",
                    Message = "User not authenticated."
                });
            }

            int ownerId = userInfo.UserId;

            if (dto.Startdate.HasValue && dto.Enddate.HasValue && dto.Startdate > dto.Enddate)
            {
                return BadRequest(new
                {
                    StatusCode = 400,
                    Status = "BadRequest",
                    Message = "Start date cannot be after end date."
                });
            }

            try
            {
                ImageService imageService = new ImageService(_environment);
                var imageUrls = new List<string>();

                if (dto.Images != null && dto.Images.Any())
                {
                    foreach (var image in dto.Images)
                    {
                        string? imageUrl = await imageService.SaveImageAsync(image);
                        if (imageUrl != null)
                            imageUrls.Add(imageUrl);
                    }
                }

                var court = new Court
                {
                    OwnerId = ownerId,
                    Location = dto.Location?.Trim(),
                    Imageurl = string.Join(",", imageUrls),
                    Priceperhour = dto.Priceperhour,
                    Status = CourtStatus.Pending,
                    Isactive = true,
                    Priority = dto.Priority,
                    Startdate = dto.Startdate,
                    Enddate = dto.Enddate
                };

                _context.Courts.Add(court);
                await _context.SaveChangesAsync();

                var history = new Courtstatushistory
                {
                    CourtId = court.CourtId,
                    Statusofcourt = court.Status,
                    Updateat = DateTime.UtcNow
                };
                _context.Courtstatushistories.Add(history);
                await _context.SaveChangesAsync();

                var owner = await _context.Accounts.FindAsync(ownerId);
                var responseData = new CourtResponseDto
                {
                    CourtId = court.CourtId,
                    OwnerId = court.OwnerId,
                    OwnerName = owner?.Fullname ?? "Unknown",
                    Location = court.Location,
                    Imageurls = court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                    Priceperhour = court.Priceperhour,
                    Status = court.Status,
                    Isactive = court.Isactive,
                    Priority = court.Priority,
                    Startdate = court.Startdate,
                    Enddate = court.Enddate
                };

                return Ok(new
                {
                    StatusCode = 200,
                    Status = "Success",
                    Message = "Court created successfully.",
                    Data = responseData
                });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new
                {
                    StatusCode = 400,
                    Status = "BadRequest",
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    StatusCode = 500,
                    Status = "InternalServerError",
                    Message = "An error occurred while creating the court.",
                    Detail = ex.Message
                });
            }
        }


        [HttpGet("{id}")]
        public async Task<ActionResult> GetById(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid court ID." });
            }

            var court = await _context.Courts
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.CourtId == id && c.Status != CourtStatus.Deleted);

            if (court == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court not found or has been deleted." });
            }

            bool isAuthenticated = User.Identity?.IsAuthenticated ?? false;
            if (court.Status == CourtStatus.Pending)
            {
                if (!isAuthenticated)
                {
                    return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this pending court." });
                }
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim, out int accountId) || (court.OwnerId != accountId && User.FindFirstValue(ClaimTypes.Role) != UserRole.Admin && User.FindFirstValue(ClaimTypes.Role) != UserRole.Staff))
                {
                    return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this pending court." });
                }
            }

            int priority = court.Priority;
            var activeOwnerPackage = await _context.Ownerpackages
                .Where(op => op.OwnerId == court.OwnerId && op.Status == "Active" && op.Startdate <= DateOnly.FromDateTime(DateTime.UtcNow) && op.Enddate >= DateOnly.FromDateTime(DateTime.UtcNow))
                .OrderBy(op => op.Priority)
                .FirstOrDefaultAsync();

            if (activeOwnerPackage != null)
            {
                priority = activeOwnerPackage.Priority;
            }

            var responseData = new CourtResponseDto
            {
                CourtId = court.CourtId,
                OwnerId = court.OwnerId,
                OwnerName = court.Owner?.Fullname ?? "Unknown",
                Location = court.Location,
                Imageurls = court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                Priceperhour = court.Priceperhour,
                Status = court.Status,
                Isactive = court.Isactive,
                Priority = priority,
                Startdate = court.Startdate,
                Enddate = court.Enddate
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Court retrieved successfully.", Data = responseData });
        }

        [HttpGet]
        public async Task<ActionResult> GetAll([FromQuery] CourtFilterParams filterParams)
        {
            // ===== Validate các tham số =====
            if (!string.IsNullOrEmpty(filterParams.Status) && !_validStatuses.Contains(filterParams.Status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Pending, Approved, Rejected." });
            }

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

            // ===== Lấy thông tin user từ claim =====
            var userInfo = UserHelper.GetUserInfo(User);
            bool isAuthenticated = userInfo.UserId > 0;
            bool isAdmin = userInfo.Role == UserRole.Admin;
            bool isStaff = userInfo.Role == UserRole.Staff;

            var query = _context.Courts
                .Include(c => c.Owner)
                .Where(c => c.Status != CourtStatus.Deleted)
                .AsQueryable();

            if (!isAuthenticated)
            {
                query = query.Where(c => c.Status == CourtStatus.Approved && c.Isactive);
            }
            else if (isAdmin)
            {
            }
            else if (isStaff)
            {
                query = query.Where(c => c.OwnerId == userInfo.UserId);
            }
            else
            {
                query = query.Where(c =>
                    c.Status == CourtStatus.Approved ||
                    (c.OwnerId == userInfo.UserId && c.Status == CourtStatus.Pending));
            }

            if (!string.IsNullOrEmpty(filterParams.Status))
            {
                query = query.Where(c => c.Status == filterParams.Status);
            }

            if (!string.IsNullOrEmpty(filterParams.Search))
            {
                query = query.Where(c => c.Location != null && c.Location.ToLower().Contains(filterParams.Search.ToLower()));
            }

            if (filterParams.MinPrice.HasValue)
            {
                query = query.Where(c => c.Priceperhour >= filterParams.MinPrice.Value);
            }

            if (filterParams.MaxPrice.HasValue)
            {
                query = query.Where(c => c.Priceperhour <= filterParams.MaxPrice.Value);
            }

            if (filterParams.StartDate.HasValue)
            {
                var startDateTime = filterParams.StartDate.Value.ToDateTime(TimeOnly.MinValue);
                query = query.Where(c => c.Startdate.HasValue && c.Startdate.Value >= startDateTime);
            }

            if (filterParams.EndDate.HasValue)
            {
                var endDateTime = filterParams.EndDate.Value.ToDateTime(TimeOnly.MaxValue);
                query = query.Where(c => c.Enddate.HasValue && c.Enddate.Value <= endDateTime);
            }

            var totalCount = await query.CountAsync();
            var sortBy = filterParams.SortBy?.ToLower() ?? "priority";
            var isDescending = filterParams.SortOrder?.ToLower() == "desc";

            query = sortBy switch
            {
                "priceperhour" => isDescending ? query.OrderByDescending(c => c.Priceperhour) : query.OrderBy(c => c.Priceperhour),
                "location" => isDescending ? query.OrderByDescending(c => c.Location) : query.OrderBy(c => c.Location),
                "startdate" => isDescending ? query.OrderByDescending(c => c.Startdate) : query.OrderBy(c => c.Startdate),
                "enddate" => isDescending ? query.OrderByDescending(c => c.Enddate) : query.OrderBy(c => c.Enddate),
                _ => isDescending ? query.OrderByDescending(c => c.Priority) : query.OrderBy(c => c.Priority)
            };

            query = query
                .Skip((filterParams.Page - 1) * filterParams.PageSize)
                .Take(filterParams.PageSize);

            var courts = await query.ToListAsync();

            var ownerIds = courts.Select(c => c.OwnerId).Distinct().ToList();
            var activeOwnerPackages = await _context.Ownerpackages
                .Where(op => ownerIds.Contains(op.OwnerId)
                             && op.Status == "Active"
                             && op.Startdate <= DateOnly.FromDateTime(DateTime.UtcNow)
                             && op.Enddate >= DateOnly.FromDateTime(DateTime.UtcNow))
                .GroupBy(op => op.OwnerId)
                .Select(g => g.OrderBy(op => op.Priority).FirstOrDefault())
                .ToDictionaryAsync(op => op.OwnerId, op => op.Priority);

            var responseData = courts.Select(court =>
            {
                int priority = court.Priority;
                if (activeOwnerPackages.TryGetValue(court.OwnerId, out int packagePriority))
                {
                    priority = packagePriority;
                }

                return new CourtResponseDto
                {
                    CourtId = court.CourtId,
                    OwnerId = court.OwnerId,
                    OwnerName = court.Owner?.Fullname ?? "Unknown",
                    Location = court.Location,
                    Imageurls = court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                    Priceperhour = court.Priceperhour,
                    Status = court.Status,
                    Isactive = court.Isactive,
                    Priority = priority,
                    Startdate = court.Startdate,
                    Enddate = court.Enddate
                };
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
                Message = "Courts retrieved successfully.",
                Pagination = paginationMetadata,
                Data = responseData
            });
        }


        [HttpGet("my")]
        [Authorize(Roles = UserRole.Customer)]
        public async Task<ActionResult> GetMyCourts([FromQuery] CourtFilterParams filterParams)
        {
            if (!string.IsNullOrEmpty(filterParams.Status) && !_validStatuses.Contains(filterParams.Status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Pending, Approved, Rejected." });
            }

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

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int ownerId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var query = _context.Courts
                .Include(c => c.Owner)
                .Where(c => c.OwnerId == ownerId && c.Status != CourtStatus.Deleted)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(filterParams.Status))
            {
                query = query.Where(c => c.Status == filterParams.Status);
            }

            // Search by location
            if (!string.IsNullOrEmpty(filterParams.Search))
            {
                query = query.Where(c => c.Location != null && c.Location.ToLower().Contains(filterParams.Search.ToLower()));
            }

            // Filter by price range
            if (filterParams.MinPrice.HasValue)
            {
                query = query.Where(c => c.Priceperhour >= filterParams.MinPrice.Value);
            }

            if (filterParams.MaxPrice.HasValue)
            {
                query = query.Where(c => c.Priceperhour <= filterParams.MaxPrice.Value);
            }

            // Filter by date range
            if (filterParams.StartDate.HasValue)
            {
                var startDateTime = filterParams.StartDate.Value.ToDateTime(TimeOnly.MinValue);
                query = query.Where(c => c.Startdate.HasValue && c.Startdate.Value >= startDateTime);
            }

            if (filterParams.EndDate.HasValue)
            {
                var endDateTime = filterParams.EndDate.Value.ToDateTime(TimeOnly.MaxValue);
                query = query.Where(c => c.Enddate.HasValue && c.Enddate.Value <= endDateTime);
            }


            // Get total count for pagination
            var totalCount = await query.CountAsync();

            // Apply sorting
            var sortBy = filterParams.SortBy?.ToLower() ?? "priority";
            var isDescending = filterParams.SortOrder?.ToLower() == "desc";

            query = sortBy switch
            {
                "priceperhour" => isDescending ? query.OrderByDescending(c => c.Priceperhour) : query.OrderBy(c => c.Priceperhour),
                "location" => isDescending ? query.OrderByDescending(c => c.Location) : query.OrderBy(c => c.Location),
                "startdate" => isDescending ? query.OrderByDescending(c => c.Startdate) : query.OrderBy(c => c.Startdate),
                "enddate" => isDescending ? query.OrderByDescending(c => c.Enddate) : query.OrderBy(c => c.Enddate),
                _ => isDescending ? query.OrderByDescending(c => c.Priority) : query.OrderBy(c => c.Priority)
            };

            // Apply pagination
            query = query
                .Skip((filterParams.Page - 1) * filterParams.PageSize)
                .Take(filterParams.PageSize);

            var courts = await query.ToListAsync();

            var activeOwnerPackage = await _context.Ownerpackages
                .Where(op => op.OwnerId == ownerId && op.Status == "Active" && op.Startdate <= DateOnly.FromDateTime(DateTime.UtcNow) && op.Enddate >= DateOnly.FromDateTime(DateTime.UtcNow))
                .OrderBy(op => op.Priority)
                .FirstOrDefaultAsync();

            var responseData = courts.Select(court =>
            {
                int priority = court.Priority;
                if (activeOwnerPackage != null)
                {
                    priority = activeOwnerPackage.Priority;
                }

                return new CourtResponseDto
                {
                    CourtId = court.CourtId,
                    OwnerId = court.OwnerId,
                    OwnerName = court.Owner?.Fullname ?? "Unknown",
                    Location = court.Location,
                    Imageurls = court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                    Priceperhour = court.Priceperhour,
                    Status = court.Status,
                    Isactive = court.Isactive,
                    Priority = priority,
                    Startdate = court.Startdate,
                    Enddate = court.Enddate
                };
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
                Message = "My courts retrieved successfully.",
                Pagination = paginationMetadata,
                Data = responseData
            });
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult> Update(int id, [FromForm] CourtUpdateDto dto)
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
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid court ID." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int accountId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var court = await _context.Courts
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.CourtId == id && c.Status != CourtStatus.Deleted);

            if (court == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court not found or has been deleted." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isOwner = court.OwnerId == accountId;
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to update this court." });
            }

            try
            {
                string oldStatus = court.Status ?? string.Empty;

                if (!string.IsNullOrEmpty(dto.Location)) court.Location = dto.Location.Trim();
                if (dto.Priceperhour.HasValue) court.Priceperhour = dto.Priceperhour.Value;
                if (dto.Priority.HasValue) court.Priority = dto.Priority.Value;
                if (dto.Startdate.HasValue) court.Startdate = dto.Startdate;
                if (dto.Enddate.HasValue) court.Enddate = dto.Enddate;

                if (dto.Images != null && dto.Images.Any())
                {
                    ImageService imageService = new ImageService(_environment);
                    var imageUrls = new List<string>();
                    foreach (var image in dto.Images)
                    {
                        string? imageUrl = await imageService.SaveImageAsync(image);
                        if (imageUrl != null)
                        {
                            imageUrls.Add(imageUrl);
                        }
                    }
                    court.Imageurl = string.Join(",", imageUrls);
                }

                if (!string.IsNullOrEmpty(dto.Status) && isAdminOrStaff)
                {
                    if (!_validStatuses.Contains(dto.Status) || dto.Status == CourtStatus.Deleted)
                    {
                        return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Pending, Approved, Rejected." });
                    }
                    court.Status = dto.Status.Trim();
                }

                if (court.Startdate.HasValue && court.Enddate.HasValue && court.Startdate > court.Enddate)
                {
                    return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Start date cannot be after end date." });
                }

                if (court.Status != oldStatus)
                {
                    var history = new Courtstatushistory
                    {
                        CourtId = court.CourtId,
                        Statusofcourt = court.Status,
                        Updateat = DateTime.UtcNow
                    };
                    _context.Courtstatushistories.Add(history);
                }

                await _context.SaveChangesAsync();

                int priority = court.Priority;
                var activeOwnerPackage = await _context.Ownerpackages
                    .Where(op => op.OwnerId == court.OwnerId && op.Status == "Active" && op.Startdate <= DateOnly.FromDateTime(DateTime.UtcNow) && op.Enddate >= DateOnly.FromDateTime(DateTime.UtcNow))
                    .OrderBy(op => op.Priority)
                    .FirstOrDefaultAsync();

                if (activeOwnerPackage != null)
                {
                    priority = activeOwnerPackage.Priority;
                }

                var responseData = new CourtResponseDto
                {
                    CourtId = court.CourtId,
                    OwnerId = court.OwnerId,
                    OwnerName = court.Owner?.Fullname ?? "Unknown",
                    Location = court.Location,
                    Imageurls = court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                    Priceperhour = court.Priceperhour,
                    Status = court.Status,
                    Isactive = court.Isactive,
                    Priority = priority,
                    Startdate = court.Startdate,
                    Enddate = court.Enddate
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Court updated successfully.", Data = responseData });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while updating the court." });
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> Delete(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid court ID." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int accountId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var court = await _context.Courts
                .FirstOrDefaultAsync(c => c.CourtId == id && c.Status != CourtStatus.Deleted);

            if (court == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court not found or has been deleted." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isOwner = court.OwnerId == accountId;
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to delete this court." });
            }

            try
            {
                string oldStatus = court.Status ?? string.Empty;
                court.Status = CourtStatus.Deleted;
                court.Isactive = false;

                var history = new Courtstatushistory
                {
                    CourtId = court.CourtId,
                    Statusofcourt = court.Status,
                    Updateat = DateTime.UtcNow
                };
                _context.Courtstatushistories.Add(history);

                await _context.SaveChangesAsync();

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Court marked as deleted successfully." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while deleting the court." });
            }
        }

        [HttpGet("{id}/feedbacks")]
        public async Task<ActionResult> GetCourtFeedbacks(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid court ID." });
            }

            var court = await _context.Courts
                .FirstOrDefaultAsync(c => c.CourtId == id && c.Status != CourtStatus.Deleted);

            if (court == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court not found or has been deleted." });
            }

            bool isAuthenticated = User.Identity?.IsAuthenticated ?? false;
            bool canView = true;
            if (court.Status == CourtStatus.Pending)
            {
                if (!isAuthenticated)
                {
                    canView = false;
                }
                else
                {
                    var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (userIdClaim == null || !int.TryParse(userIdClaim, out int accountId))
                    {
                        canView = false;
                    }
                    else
                    {
                        var userRole = User.FindFirstValue(ClaimTypes.Role);
                        canView = court.OwnerId == accountId || userRole == UserRole.Admin || userRole == UserRole.Staff;
                    }
                }
            }

            if (!canView)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view feedbacks for this court." });
            }

            var feedbacks = await _context.Feedbacks
                .Include(f => f.Booking)
                .Where(f => f.Booking.CourtId == id)
                .ToListAsync();

            var responseData = feedbacks.Select(f => new FeedbackResponseDto
            {
                FeedbackId = f.FeedbackId,
                BookingId = f.BookingId,
                UserId = f.UserId,
                Rating = f.Rating,
                Comment = f.Comment,
                Createat = f.Createat
            }).ToList();

            int totalFeedbacks = feedbacks.Count(f => f.Rating.HasValue);
            double averageRating = totalFeedbacks > 0 ? feedbacks.Where(f => f.Rating.HasValue).Average(f => (double)f.Rating.Value) : 0;

            var summary = new
            {
                TotalFeedbacks = totalFeedbacks,
                AverageRating = Math.Round(averageRating, 2)
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Court feedbacks retrieved successfully.", Summary = summary, Data = responseData });
        }
    }
}