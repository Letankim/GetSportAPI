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
    public class PlaymatePostController : ControllerBase
    {
        private readonly GetSportContext _context;
        private readonly string[] _validStatuses = { "Open", "Closed", "Cancelled" };
        private readonly string[] _validSortFields = { "Createdat", "Neededplayers", "UserName", "CourtName", "CourtLocation", "Bookingdate", "SlotStarttime" };

        public PlaymatePostController(GetSportContext context)
        {
            _context = context;
        }


        [HttpPost]
        [Authorize(Roles = $"{UserRole.Customer}")]
        public async Task<ActionResult> Create([FromBody] PlaymatePostCreateDto dto)
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

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            if (!_validStatuses.Contains(dto.Status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = $"Invalid status. Allowed values are: {string.Join(", ", _validStatuses)}." });
            }

            var booking = await _context.Courtbookings
                .Include(b => b.Court)
                    .ThenInclude(c => c.Owner)
                .Include(b => b.Slot)
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.BookingId == dto.CourtbookingId && b.UserId == userId && b.Status == "Confirmed");

            if (booking == null)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid booking, not owned by user, or not confirmed." });
            }

            var slotStartTime = booking.Slot.Starttime;
            var currentTime = DateTime.UtcNow;
            if (slotStartTime <= currentTime.AddHours(1))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Cannot create post for a booking that is expired or starts within 1 hour." });
            }

            var existingPost = await _context.Playmateposts
                .FirstOrDefaultAsync(p => p.CourtbookingId == dto.CourtbookingId);
            if (existingPost != null)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "A playmate post already exists for this booking." });
            }

            try
            {
                var post = new Playmatepost
                {
                    UserId = userId,
                    CourtbookingId = dto.CourtbookingId,
                    Title = dto.Title.Trim(),
                    Content = dto.Content?.Trim(),
                    Neededplayers = dto.Neededplayers,
                    Skilllevel = dto.Skilllevel?.Trim(),
                    Status = dto.Status.Trim(),
                    Createdat = DateTime.UtcNow
                };

                _context.Playmateposts.Add(post);
                await _context.SaveChangesAsync();

                var responseData = new PlaymatePostResponseDto
                {
                    PostId = post.PostId,
                    UserId = post.UserId,
                    UserName = booking.User?.Fullname ?? "Unknown",
                    CourtbookingId = post.CourtbookingId.Value,
                    CourtId = booking.CourtId,
                    CourtName = booking.Court.Owner.Fullname ?? "Unknown",
                    CourtLocation = booking.Court.Location ?? "Unknown",
                    CourtImageUrls = booking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                    Bookingdate = booking.Bookingdate,
                    SlotStarttime = booking.Slot.Starttime,
                    SlotEndtime = booking.Slot.Endtime,
                    Title = post.Title,
                    Content = post.Content,
                    Neededplayers = post.Neededplayers,
                    CurrentPlayers = 0,
                    Skilllevel = post.Skilllevel,
                    Status = post.Status,
                    Createdat = post.Createdat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Playmate post created successfully.", Data = responseData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while creating the playmate post: {ex.Message}" });
            }
        }

        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> GetAll([FromQuery] PlaymatePostFilterParams filterParams)
        {
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

            if (!string.IsNullOrEmpty(filterParams.Status) && !_validStatuses.Contains(filterParams.Status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = $"Invalid status. Allowed values are: {string.Join(", ", _validStatuses)}." });
            }

            if (filterParams.MinNeededPlayers.HasValue && filterParams.MinNeededPlayers < 1)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Minimum needed players must be greater than 0." });
            }

            if (filterParams.MaxNeededPlayers.HasValue && filterParams.MaxNeededPlayers < 1)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Maximum needed players must be greater than 0." });
            }

            if (filterParams.MinNeededPlayers.HasValue && filterParams.MaxNeededPlayers.HasValue && filterParams.MinNeededPlayers > filterParams.MaxNeededPlayers)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Minimum needed players cannot be greater than maximum needed players." });
            }

            var currentTime = DateTime.UtcNow;
            var query = _context.Playmateposts
                .Include(p => p.User)
                .Include(p => p.Courtbooking)
                .ThenInclude(b => b.Court)
                .ThenInclude(c => c.Owner)
                .Include(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .Include(p => p.Playmatejoins)
                .Where(p => p.Courtbooking != null && p.Courtbooking.Status == "Confirmed" && p.Courtbooking.Slot.Starttime > currentTime.AddHours(1))
                .AsQueryable();

            // Apply filters
            if (filterParams.CourtbookingId.HasValue)
            {
                query = query.Where(p => p.CourtbookingId == filterParams.CourtbookingId.Value);
            }

            if (filterParams.UserId.HasValue)
            {
                query = query.Where(p => p.UserId == filterParams.UserId.Value);
            }

            if (!string.IsNullOrEmpty(filterParams.Status))
            {
                query = query.Where(p => p.Status == filterParams.Status);
            }

            if (!string.IsNullOrEmpty(filterParams.Skilllevel))
            {
                var skillLevelLower = filterParams.Skilllevel.ToLower();
                query = query.Where(p => p.Skilllevel != null && p.Skilllevel.ToLower().Contains(skillLevelLower));
            }

            if (filterParams.MinNeededPlayers.HasValue)
            {
                query = query.Where(p => p.Neededplayers >= filterParams.MinNeededPlayers.Value);
            }

            if (filterParams.MaxNeededPlayers.HasValue)
            {
                query = query.Where(p => p.Neededplayers <= filterParams.MaxNeededPlayers.Value);
            }

            if (filterParams.StartCreateDate.HasValue)
            {
                query = query.Where(p => p.Createdat >= filterParams.StartCreateDate.Value);
            }

            if (filterParams.EndCreateDate.HasValue)
            {
                query = query.Where(p => p.Createdat <= filterParams.EndCreateDate.Value);
            }

            if (!string.IsNullOrEmpty(filterParams.Search))
            {
                var searchLower = filterParams.Search.ToLower();
                query = query.Where(p => (p.User.Fullname != null && p.User.Fullname.ToLower().Contains(searchLower)) ||
                                        (p.Courtbooking.Court.Owner.Fullname != null && p.Courtbooking.Court.Owner.Fullname.ToLower().Contains(searchLower)) ||
                                        (p.Courtbooking.Court.Location != null && p.Courtbooking.Court.Location.ToLower().Contains(searchLower)));
            }

            // Get total count for pagination
            var totalCount = await query.CountAsync();

            // Apply sorting
            var sortBy = filterParams.SortBy?.ToLower() ?? "createdat";
            var isDescending = filterParams.SortOrder?.ToLower() == "desc";

            query = sortBy switch
            {
                "neededplayers" => isDescending ? query.OrderByDescending(p => p.Neededplayers) : query.OrderBy(p => p.Neededplayers),
                "username" => isDescending ? query.OrderByDescending(p => p.User.Fullname) : query.OrderBy(p => p.User.Fullname),
                "courtname" => isDescending ? query.OrderByDescending(p => p.Courtbooking.Court.Owner.Fullname) : query.OrderBy(p => p.Courtbooking.Court.Owner.Fullname),
                "courtlocation" => isDescending ? query.OrderByDescending(p => p.Courtbooking.Court.Location) : query.OrderBy(p => p.Courtbooking.Court.Location),
                "bookingdate" => isDescending ? query.OrderByDescending(p => p.Courtbooking.Bookingdate) : query.OrderBy(p => p.Courtbooking.Bookingdate),
                "slotstarttime" => isDescending ? query.OrderByDescending(p => p.Courtbooking.Slot.Starttime) : query.OrderBy(p => p.Courtbooking.Slot.Starttime),
                _ => isDescending ? query.OrderByDescending(p => p.Createdat) : query.OrderBy(p => p.Createdat)
            };

            // Apply pagination
            query = query
                .Skip((filterParams.Page - 1) * filterParams.PageSize)
                .Take(filterParams.PageSize);

            var posts = await query.ToListAsync();

            var responseData = posts.Select(post => new PlaymatePostResponseDto
            {
                PostId = post.PostId,
                UserId = post.UserId,
                UserName = post.User?.Fullname ?? "Unknown",
                CourtbookingId = post.CourtbookingId.Value,
                CourtId = post.Courtbooking.CourtId,
                CourtName = post.Courtbooking.Court.Owner.Fullname ?? "Unknown",
                CourtLocation = post.Courtbooking.Court.Location ?? "Unknown",
                CourtImageUrls = post.Courtbooking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                Bookingdate = post.Courtbooking.Bookingdate,
                SlotStarttime = post.Courtbooking.Slot.Starttime,
                SlotEndtime = post.Courtbooking.Slot.Endtime,
                Title = post.Title,
                Content = post.Content,
                Neededplayers = post.Neededplayers,
                CurrentPlayers = post.Playmatejoins?.Count ?? 0,
                Skilllevel = post.Skilllevel,
                Status = post.Status,
                Createdat = post.Createdat
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
                Message = "Playmate posts retrieved successfully.",
                Pagination = paginationMetadata,
                Data = responseData
            });
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult> GetById(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid post ID." });
            }

            var post = await _context.Playmateposts
                .Include(p => p.User)
                .Include(p => p.Courtbooking)
                .ThenInclude(b => b.Court)
                .ThenInclude(c => c.Owner)
                .Include(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .Include(p => p.Playmatejoins)
                .FirstOrDefaultAsync(p => p.PostId == id);

            if (post == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Playmate post not found." });
            }

            if (post.Courtbooking == null || post.Courtbooking.Status != "Confirmed" || post.Courtbooking.Slot.Starttime <= DateTime.UtcNow.AddHours(1))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Playmate post is associated with an invalid or expired booking." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == post.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this playmate post." });
            }

            var responseData = new PlaymatePostResponseDto
            {
                PostId = post.PostId,
                UserId = post.UserId,
                UserName = post.User?.Fullname ?? "Unknown",
                CourtbookingId = post.CourtbookingId.Value,
                CourtId = post.Courtbooking.CourtId,
                CourtName = post.Courtbooking.Court.Owner.Fullname ?? "Unknown",
                CourtLocation = post.Courtbooking.Court.Location ?? "Unknown",
                CourtImageUrls = post.Courtbooking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                Bookingdate = post.Courtbooking.Bookingdate,
                SlotStarttime = post.Courtbooking.Slot.Starttime,
                SlotEndtime = post.Courtbooking.Slot.Endtime,
                Title = post.Title,
                Content = post.Content,
                Neededplayers = post.Neededplayers,
                CurrentPlayers = post.Playmatejoins?.Count ?? 0,
                Skilllevel = post.Skilllevel,
                Status = post.Status,
                Createdat = post.Createdat
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Playmate post retrieved successfully.", Data = responseData });
        }

        [HttpGet("my")]
        [Authorize(Roles = $"{UserRole.Customer}")]
        public async Task<ActionResult> GetMyPosts([FromQuery] PlaymatePostFilterParams filterParams)
        {
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

            if (!string.IsNullOrEmpty(filterParams.Status) && !_validStatuses.Contains(filterParams.Status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = $"Invalid status. Allowed values are: {string.Join(", ", _validStatuses)}." });
            }

            if (filterParams.MinNeededPlayers.HasValue && filterParams.MinNeededPlayers < 1)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Minimum needed players must be greater than 0." });
            }

            if (filterParams.MaxNeededPlayers.HasValue && filterParams.MaxNeededPlayers < 1)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Maximum needed players must be greater than 0." });
            }

            if (filterParams.MinNeededPlayers.HasValue && filterParams.MaxNeededPlayers.HasValue && filterParams.MinNeededPlayers > filterParams.MaxNeededPlayers)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Minimum needed players cannot be greater than maximum needed players." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var currentTime = DateTime.UtcNow;
            var query = _context.Playmateposts
                .Include(p => p.User)
                .Include(p => p.Courtbooking)
                .ThenInclude(b => b.Court)
                .ThenInclude(c => c.Owner)
                .Include(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .Include(p => p.Playmatejoins)
                .Where(p => p.UserId == userId && p.Courtbooking != null && p.Courtbooking.Status == "Confirmed" && p.Courtbooking.Slot.Starttime > currentTime.AddHours(1))
                .AsQueryable();

            // Apply filters
            if (filterParams.CourtbookingId.HasValue)
            {
                query = query.Where(p => p.CourtbookingId == filterParams.CourtbookingId.Value);
            }

            if (!string.IsNullOrEmpty(filterParams.Status))
            {
                query = query.Where(p => p.Status == filterParams.Status);
            }

            if (!string.IsNullOrEmpty(filterParams.Skilllevel))
            {
                var skillLevelLower = filterParams.Skilllevel.ToLower();
                query = query.Where(p => p.Skilllevel != null && p.Skilllevel.ToLower().Contains(skillLevelLower));
            }

            if (filterParams.MinNeededPlayers.HasValue)
            {
                query = query.Where(p => p.Neededplayers >= filterParams.MinNeededPlayers.Value);
            }

            if (filterParams.MaxNeededPlayers.HasValue)
            {
                query = query.Where(p => p.Neededplayers <= filterParams.MaxNeededPlayers.Value);
            }

            if (filterParams.StartCreateDate.HasValue)
            {
                query = query.Where(p => p.Createdat >= filterParams.StartCreateDate.Value);
            }

            if (filterParams.EndCreateDate.HasValue)
            {
                query = query.Where(p => p.Createdat <= filterParams.EndCreateDate.Value);
            }

            if (!string.IsNullOrEmpty(filterParams.Search))
            {
                var searchLower = filterParams.Search.ToLower();
                query = query.Where(p => (p.User.Fullname != null && p.User.Fullname.ToLower().Contains(searchLower)) ||
                                        (p.Courtbooking.Court.Owner.Fullname != null && p.Courtbooking.Court.Owner.Fullname.ToLower().Contains(searchLower)) ||
                                        (p.Courtbooking.Court.Location != null && p.Courtbooking.Court.Location.ToLower().Contains(searchLower)));
            }

            // Get total count for pagination
            var totalCount = await query.CountAsync();

            // Apply sorting
            var sortBy = filterParams.SortBy?.ToLower() ?? "createdat";
            var isDescending = filterParams.SortOrder?.ToLower() == "desc";

            query = sortBy switch
            {
                "neededplayers" => isDescending ? query.OrderByDescending(p => p.Neededplayers) : query.OrderBy(p => p.Neededplayers),
                "username" => isDescending ? query.OrderByDescending(p => p.User.Fullname) : query.OrderBy(p => p.User.Fullname),
                "courtname" => isDescending ? query.OrderByDescending(p => p.Courtbooking.Court.Owner.Fullname) : query.OrderBy(p => p.Courtbooking.Court.Owner.Fullname),
                "courtlocation" => isDescending ? query.OrderByDescending(p => p.Courtbooking.Court.Location) : query.OrderBy(p => p.Courtbooking.Court.Location),
                "bookingdate" => isDescending ? query.OrderByDescending(p => p.Courtbooking.Bookingdate) : query.OrderBy(p => p.Courtbooking.Bookingdate),
                "slotstarttime" => isDescending ? query.OrderByDescending(p => p.Courtbooking.Slot.Starttime) : query.OrderBy(p => p.Courtbooking.Slot.Starttime),
                _ => isDescending ? query.OrderByDescending(p => p.Createdat) : query.OrderBy(p => p.Createdat)
            };

            // Apply pagination
            query = query
                .Skip((filterParams.Page - 1) * filterParams.PageSize)
                .Take(filterParams.PageSize);

            var posts = await query.ToListAsync();

            var responseData = posts.Select(post => new PlaymatePostResponseDto
            {
                PostId = post.PostId,
                UserId = post.UserId,
                UserName = post.User?.Fullname ?? "Unknown",
                CourtbookingId = post.CourtbookingId.Value,
                CourtId = post.Courtbooking.CourtId,
                CourtName = post.Courtbooking.Court.Owner.Fullname ?? "Unknown",
                CourtLocation = post.Courtbooking.Court.Location ?? "Unknown",
                CourtImageUrls = post.Courtbooking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                Bookingdate = post.Courtbooking.Bookingdate,
                SlotStarttime = post.Courtbooking.Slot.Starttime,
                SlotEndtime = post.Courtbooking.Slot.Endtime,
                Title = post.Title,
                Content = post.Content,
                Neededplayers = post.Neededplayers,
                CurrentPlayers = post.Playmatejoins?.Count ?? 0,
                Skilllevel = post.Skilllevel,
                Status = post.Status,
                Createdat = post.Createdat
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
                Message = "Your playmate posts retrieved successfully.",
                Pagination = paginationMetadata,
                Data = responseData
            });
        }

        [HttpGet("court/{courtId}")]
        [Authorize]
        public async Task<ActionResult> GetByCourt(int courtId, [FromQuery] PlaymatePostFilterParams filterParams)
        {
            if (courtId <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid court ID." });
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

            if (!string.IsNullOrEmpty(filterParams.Status) && !_validStatuses.Contains(filterParams.Status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = $"Invalid status. Allowed values are: {string.Join(", ", _validStatuses)}." });
            }

            if (filterParams.MinNeededPlayers.HasValue && filterParams.MinNeededPlayers < 1)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Minimum needed players must be greater than 0." });
            }

            if (filterParams.MaxNeededPlayers.HasValue && filterParams.MaxNeededPlayers < 1)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Maximum needed players must be greater than 0." });
            }

            if (filterParams.MinNeededPlayers.HasValue && filterParams.MaxNeededPlayers.HasValue && filterParams.MinNeededPlayers > filterParams.MaxNeededPlayers)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Minimum needed players cannot be greater than maximum needed players." });
            }

            var court = await _context.Courts
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.CourtId == courtId && c.Status != CourtStatus.Deleted && c.Isactive && c.Status == CourtStatus.Approved);

            if (court == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court not found, deleted, inactive, or not approved." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var currentTime = DateTime.UtcNow;
            var query = _context.Playmateposts
                .Include(p => p.User)
                .Include(p => p.Courtbooking)
                .ThenInclude(b => b.Court)
                .ThenInclude(c => c.Owner)
                .Include(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .Include(p => p.Playmatejoins)
                .Where(p => p.Courtbooking != null && p.Courtbooking.CourtId == courtId && p.Courtbooking.Status == "Confirmed" && p.Courtbooking.Slot.Starttime > currentTime.AddHours(1))
                .AsQueryable();

            // Apply filters
            if (filterParams.CourtbookingId.HasValue)
            {
                query = query.Where(p => p.CourtbookingId == filterParams.CourtbookingId.Value);
            }

            if (filterParams.UserId.HasValue)
            {
                query = query.Where(p => p.UserId == filterParams.UserId.Value);
            }

            if (!string.IsNullOrEmpty(filterParams.Status))
            {
                query = query.Where(p => p.Status == filterParams.Status);
            }

            if (!string.IsNullOrEmpty(filterParams.Skilllevel))
            {
                var skillLevelLower = filterParams.Skilllevel.ToLower();
                query = query.Where(p => p.Skilllevel != null && p.Skilllevel.ToLower().Contains(skillLevelLower));
            }

            if (filterParams.MinNeededPlayers.HasValue)
            {
                query = query.Where(p => p.Neededplayers >= filterParams.MinNeededPlayers.Value);
            }

            if (filterParams.MaxNeededPlayers.HasValue)
            {
                query = query.Where(p => p.Neededplayers <= filterParams.MaxNeededPlayers.Value);
            }

            if (filterParams.StartCreateDate.HasValue)
            {
                query = query.Where(p => p.Createdat >= filterParams.StartCreateDate.Value);
            }

            if (filterParams.EndCreateDate.HasValue)
            {
                query = query.Where(p => p.Createdat <= filterParams.EndCreateDate.Value);
            }

            if (!string.IsNullOrEmpty(filterParams.Search))
            {
                var searchLower = filterParams.Search.ToLower();
                query = query.Where(p => (p.User.Fullname != null && p.User.Fullname.ToLower().Contains(searchLower)) ||
                                        (p.Courtbooking.Court.Owner.Fullname != null && p.Courtbooking.Court.Owner.Fullname.ToLower().Contains(searchLower)) ||
                                        (p.Courtbooking.Court.Location != null && p.Courtbooking.Court.Location.ToLower().Contains(searchLower)));
            }

            // Get total count for pagination
            var totalCount = await query.CountAsync();

            // Apply sorting
            var sortBy = filterParams.SortBy?.ToLower() ?? "createdat";
            var isDescending = filterParams.SortOrder?.ToLower() == "desc";

            query = sortBy switch
            {
                "neededplayers" => isDescending ? query.OrderByDescending(p => p.Neededplayers) : query.OrderBy(p => p.Neededplayers),
                "username" => isDescending ? query.OrderByDescending(p => p.User.Fullname) : query.OrderBy(p => p.User.Fullname),
                "courtname" => isDescending ? query.OrderByDescending(p => p.Courtbooking.Court.Owner.Fullname) : query.OrderBy(p => p.Courtbooking.Court.Owner.Fullname),
                "courtlocation" => isDescending ? query.OrderByDescending(p => p.Courtbooking.Court.Location) : query.OrderBy(p => p.Courtbooking.Court.Location),
                "bookingdate" => isDescending ? query.OrderByDescending(p => p.Courtbooking.Bookingdate) : query.OrderBy(p => p.Courtbooking.Bookingdate),
                "slotstarttime" => isDescending ? query.OrderByDescending(p => p.Courtbooking.Slot.Starttime) : query.OrderBy(p => p.Courtbooking.Slot.Starttime),
                _ => isDescending ? query.OrderByDescending(p => p.Createdat) : query.OrderBy(p => p.Createdat)
            };

            // Apply pagination
            query = query
                .Skip((filterParams.Page - 1) * filterParams.PageSize)
                .Take(filterParams.PageSize);

            var posts = await query.ToListAsync();

            var responseData = posts.Select(post => new PlaymatePostResponseDto
            {
                PostId = post.PostId,
                UserId = post.UserId,
                UserName = post.User?.Fullname ?? "Unknown",
                CourtbookingId = post.CourtbookingId.Value,
                CourtId = post.Courtbooking.CourtId,
                CourtName = post.Courtbooking.Court.Owner.Fullname ?? "Unknown",
                CourtLocation = post.Courtbooking.Court.Location ?? "Unknown",
                CourtImageUrls = post.Courtbooking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                Bookingdate = post.Courtbooking.Bookingdate,
                SlotStarttime = post.Courtbooking.Slot.Starttime,
                SlotEndtime = post.Courtbooking.Slot.Endtime,
                Title = post.Title,
                Content = post.Content,
                Neededplayers = post.Neededplayers,
                CurrentPlayers = post.Playmatejoins?.Count ?? 0,
                Skilllevel = post.Skilllevel,
                Status = post.Status,
                Createdat = post.Createdat
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
                Message = "Playmate posts for the court retrieved successfully.",
                Pagination = paginationMetadata,
                Data = responseData
            });
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult> Update(int id, [FromBody] PlaymatePostUpdateDto dto)
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
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid post ID." });
            }

            var post = await _context.Playmateposts
                .Include(p => p.User)
                .Include(p => p.Courtbooking)
                .ThenInclude(b => b.Court)
                .ThenInclude(c => c.Owner)
                .Include(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .Include(p => p.Playmatejoins)
                .FirstOrDefaultAsync(p => p.PostId == id);

            if (post == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Playmate post not found." });
            }

            if (post.Courtbooking == null || post.Courtbooking.Status != "Confirmed" || post.Courtbooking.Slot.Starttime <= DateTime.UtcNow.AddHours(1))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Playmate post is associated with an invalid or expired booking." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == post.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to update this playmate post." });
            }

            if (!string.IsNullOrEmpty(dto.Status) && !_validStatuses.Contains(dto.Status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = $"Invalid status. Allowed values are: {string.Join(", ", _validStatuses)}." });
            }

            try
            {
                if (!string.IsNullOrEmpty(dto.Title)) post.Title = dto.Title.Trim();
                if (!string.IsNullOrEmpty(dto.Content)) post.Content = dto.Content?.Trim();
                if (dto.Neededplayers.HasValue) post.Neededplayers = dto.Neededplayers.Value;
                if (!string.IsNullOrEmpty(dto.Skilllevel)) post.Skilllevel = dto.Skilllevel?.Trim();
                if (!string.IsNullOrEmpty(dto.Status)) post.Status = dto.Status.Trim();

                await _context.SaveChangesAsync();

                var responseData = new PlaymatePostResponseDto
                {
                    PostId = post.PostId,
                    UserId = post.UserId,
                    UserName = post.User?.Fullname ?? "Unknown",
                    CourtbookingId = post.CourtbookingId.Value,
                    CourtId = post.Courtbooking.CourtId,
                    CourtName = post.Courtbooking.Court.Owner.Fullname ?? "Unknown",
                    CourtLocation = post.Courtbooking.Court.Location ?? "Unknown",
                    CourtImageUrls = post.Courtbooking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                    Bookingdate = post.Courtbooking.Bookingdate,
                    SlotStarttime = post.Courtbooking.Slot.Starttime,
                    SlotEndtime = post.Courtbooking.Slot.Endtime,
                    Title = post.Title,
                    Content = post.Content,
                    Neededplayers = post.Neededplayers,
                    CurrentPlayers = post.Playmatejoins?.Count ?? 0,
                    Skilllevel = post.Skilllevel,
                    Status = post.Status,
                    Createdat = post.Createdat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Playmate post updated successfully.", Data = responseData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while updating the playmate post: {ex.Message}" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> Delete(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid post ID." });
            }

            var post = await _context.Playmateposts.FirstOrDefaultAsync(p => p.PostId == id);

            if (post == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Playmate post not found." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == post.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to delete this playmate post." });
            }

            try
            {
                _context.Playmateposts.Remove(post);
                await _context.SaveChangesAsync();

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Playmate post deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while deleting the playmate post: {ex.Message}" });
            }
        }
    }
}