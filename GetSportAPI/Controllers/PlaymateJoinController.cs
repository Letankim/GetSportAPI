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
    public class PlaymateJoinController : ControllerBase
    {
        private readonly GetSportContext _context;
        private readonly string[] _validSortFields = { "Joinedat", "UserName", "CourtName", "CourtLocation", "Bookingdate", "SlotStarttime" };

        public PlaymateJoinController(GetSportContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize(Roles = $"{UserRole.Customer}")]
        public async Task<ActionResult> Create([FromBody] PlaymateJoinCreateDto dto)
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

            var post = await _context.Playmateposts
                .Include(p => p.Playmatejoins)
                .Include(p => p.Courtbooking)
                .ThenInclude(b => b.Court)
                .ThenInclude(c => c.Owner)
                .Include(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.PostId == dto.PostId);

            if (post == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Playmate post not found." });
            }

            if (post.Courtbooking == null || post.Courtbooking.Status != "Confirmed" || post.Courtbooking.Slot.Starttime <= DateTime.UtcNow.AddHours(1))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Playmate post is associated with an invalid or expired booking." });
            }

            if (post.Status != "Open")
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Playmate post is not open for joining." });
            }

            var currentJoins = post.Playmatejoins.Count;
            if (currentJoins >= post.Neededplayers)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Playmate post has reached its maximum number of players." });
            }

            if (post.UserId == userId)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "You cannot join your own playmate post." });
            }

            if (post.Playmatejoins.Any(j => j.UserId == userId))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "You have already joined this playmate post." });
            }

            try
            {
                var join = new Playmatejoin
                {
                    PostId = dto.PostId,
                    UserId = userId,
                    Joinedat = DateTime.UtcNow
                };

                _context.Playmatejoins.Add(join);
                await _context.SaveChangesAsync();

                if (post.Playmatejoins.Count + 1 >= post.Neededplayers)
                {
                    post.Status = "Closed";
                    await _context.SaveChangesAsync();
                }

                var responseData = new PlaymateJoinResponseDto
                {
                    JoinId = join.JoinId,
                    PostId = join.PostId,
                    CourtbookingId = post.CourtbookingId.Value,
                    CourtId = post.Courtbooking.CourtId,
                    CourtName = post.Courtbooking.Court.Owner.Fullname ?? "Unknown",
                    CourtLocation = post.Courtbooking.Court.Location ?? "Unknown",
                    CourtImageUrls = post.Courtbooking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                    Bookingdate = post.Courtbooking.Bookingdate,
                    SlotStarttime = post.Courtbooking.Slot.Starttime,
                    SlotEndtime = post.Courtbooking.Slot.Endtime,
                    PostTitle = post.Title,
                    PostSkilllevel = post.Skilllevel,
                    PostStatus = post.Status,
                    Neededplayers = post.Neededplayers,
                    CurrentPlayers = post.Playmatejoins.Count + 1,
                    UserId = join.UserId,
                    UserName = post.User?.Fullname ?? "Unknown",
                    Joinedat = join.Joinedat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Successfully joined the playmate post.", Data = responseData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while joining the playmate post: {ex.Message}" });
            }
        }

        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> GetAll([FromQuery] PlaymateJoinFilterParams filterParams)
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

            var query = _context.Playmatejoins
                .Include(j => j.User)
                .Include(j => j.Post)
                .ThenInclude(p => p.Courtbooking)
                .ThenInclude(b => b.Court)
                .ThenInclude(c => c.Owner)
                .Include(j => j.Post)
                .ThenInclude(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .Include(j => j.Post)
                .ThenInclude(p => p.Playmatejoins)
                .Where(j => j.Post.Courtbooking != null && j.Post.Courtbooking.Status == "Confirmed" && j.Post.Courtbooking.Slot.Starttime > DateTime.UtcNow.AddHours(1))
                .AsQueryable();

            // Apply filters
            if (filterParams.PostId.HasValue)
            {
                query = query.Where(j => j.PostId == filterParams.PostId.Value);
            }

            if (filterParams.UserId.HasValue)
            {
                query = query.Where(j => j.UserId == filterParams.UserId.Value);
            }

            if (filterParams.StartJoinedDate.HasValue)
            {
                query = query.Where(j => j.Joinedat >= filterParams.StartJoinedDate.Value);
            }

            if (filterParams.EndJoinedDate.HasValue)
            {
                query = query.Where(j => j.Joinedat <= filterParams.EndJoinedDate.Value);
            }

            if (!string.IsNullOrEmpty(filterParams.Search))
            {
                var searchLower = filterParams.Search.ToLower();
                query = query.Where(j => (j.User.Fullname != null && j.User.Fullname.ToLower().Contains(searchLower)) ||
                                        (j.Post.Courtbooking.Court.Owner.Fullname != null && j.Post.Courtbooking.Court.Owner.Fullname.ToLower().Contains(searchLower)) ||
                                        (j.Post.Courtbooking.Court.Location != null && j.Post.Courtbooking.Court.Location.ToLower().Contains(searchLower)));
            }

            var totalCount = await query.CountAsync();

            // Apply 
            var sortBy = filterParams.SortBy?.ToLower() ?? "joinedat";
            var isDescending = filterParams.SortOrder?.ToLower() == "desc";

            query = sortBy switch
            {
                "username" => isDescending ? query.OrderByDescending(j => j.User.Fullname) : query.OrderBy(j => j.User.Fullname),
                "courtname" => isDescending ? query.OrderByDescending(j => j.Post.Courtbooking.Court.Owner.Fullname) : query.OrderBy(j => j.Post.Courtbooking.Court.Owner.Fullname),
                "courtlocation" => isDescending ? query.OrderByDescending(j => j.Post.Courtbooking.Court.Location) : query.OrderBy(j => j.Post.Courtbooking.Court.Location),
                "bookingdate" => isDescending ? query.OrderByDescending(j => j.Post.Courtbooking.Bookingdate) : query.OrderBy(j => j.Post.Courtbooking.Bookingdate),
                "slotstarttime" => isDescending ? query.OrderByDescending(j => j.Post.Courtbooking.Slot.Starttime) : query.OrderBy(j => j.Post.Courtbooking.Slot.Starttime),
                _ => isDescending ? query.OrderByDescending(j => j.Joinedat) : query.OrderBy(j => j.Joinedat)
            };

            // Apply pagination
            query = query
                .Skip((filterParams.Page - 1) * filterParams.PageSize)
                .Take(filterParams.PageSize);

            var joins = await query.ToListAsync();

            var responseData = joins.Select(join => new PlaymateJoinResponseDto
            {
                JoinId = join.JoinId,
                PostId = join.PostId,
                CourtbookingId = join.Post.CourtbookingId.Value,
                CourtId = join.Post.Courtbooking.CourtId,
                CourtName = join.Post.Courtbooking.Court.Owner.Fullname ?? "Unknown",
                CourtLocation = join.Post.Courtbooking.Court.Location ?? "Unknown",
                CourtImageUrls = join.Post.Courtbooking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                Bookingdate = join.Post.Courtbooking.Bookingdate,
                SlotStarttime = join.Post.Courtbooking.Slot.Starttime,
                SlotEndtime = join.Post.Courtbooking.Slot.Endtime,
                PostTitle = join.Post.Title,
                PostSkilllevel = join.Post.Skilllevel,
                PostStatus = join.Post.Status,
                Neededplayers = join.Post.Neededplayers,
                CurrentPlayers = join.Post.Playmatejoins?.Count ?? 0,
                UserId = join.UserId,
                UserName = join.User?.Fullname ?? "Unknown",
                Joinedat = join.Joinedat
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
                Message = "Playmate joins retrieved successfully.",
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
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid join ID." });
            }

            var join = await _context.Playmatejoins
                .Include(j => j.User)
                .Include(j => j.Post)
                .ThenInclude(p => p.Courtbooking)
                .ThenInclude(b => b.Court)
                .ThenInclude(c => c.Owner)
                .Include(j => j.Post)
                .ThenInclude(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .Include(j => j.Post)
                .ThenInclude(p => p.Playmatejoins)
                .FirstOrDefaultAsync(j => j.JoinId == id);

            if (join == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Playmate join not found." });
            }

            if (join.Post.Courtbooking == null || join.Post.Courtbooking.Status != "Confirmed" || join.Post.Courtbooking.Slot.Starttime <= DateTime.UtcNow.AddHours(1))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Playmate join is associated with an invalid or expired booking." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == join.UserId || currentUserId == join.Post.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this playmate join." });
            }

            var responseData = new PlaymateJoinResponseDto
            {
                JoinId = join.JoinId,
                PostId = join.PostId,
                CourtbookingId = join.Post.CourtbookingId.Value,
                CourtId = join.Post.Courtbooking.CourtId,
                CourtName = join.Post.Courtbooking.Court.Owner.Fullname ?? "Unknown",
                CourtLocation = join.Post.Courtbooking.Court.Location ?? "Unknown",
                CourtImageUrls = join.Post.Courtbooking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                Bookingdate = join.Post.Courtbooking.Bookingdate,
                SlotStarttime = join.Post.Courtbooking.Slot.Starttime,
                SlotEndtime = join.Post.Courtbooking.Slot.Endtime,
                PostTitle = join.Post.Title,
                PostSkilllevel = join.Post.Skilllevel,
                PostStatus = join.Post.Status,
                Neededplayers = join.Post.Neededplayers,
                CurrentPlayers = join.Post.Playmatejoins?.Count ?? 0,
                UserId = join.UserId,
                UserName = join.User?.Fullname ?? "Unknown",
                Joinedat = join.Joinedat
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Playmate join retrieved successfully.", Data = responseData });
        }

        [HttpGet("my")]
        [Authorize(Roles = $"{UserRole.Customer}")]
        public async Task<ActionResult> GetMyJoins([FromQuery] PlaymateJoinFilterParams filterParams)
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

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var query = _context.Playmatejoins
                .Include(j => j.User)
                .Include(j => j.Post)
                .ThenInclude(p => p.Courtbooking)
                .ThenInclude(b => b.Court)
                .ThenInclude(c => c.Owner)
                .Include(j => j.Post)
                .ThenInclude(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .Include(j => j.Post)
                .ThenInclude(p => p.Playmatejoins)
                .Where(j => j.UserId == userId && j.Post.Courtbooking != null && j.Post.Courtbooking.Status == "Confirmed" && j.Post.Courtbooking.Slot.Starttime > DateTime.UtcNow.AddHours(1))
                .AsQueryable();

            // Apply filters
            if (filterParams.PostId.HasValue)
            {
                query = query.Where(j => j.PostId == filterParams.PostId.Value);
            }

            if (filterParams.StartJoinedDate.HasValue)
            {
                query = query.Where(j => j.Joinedat >= filterParams.StartJoinedDate.Value);
            }

            if (filterParams.EndJoinedDate.HasValue)
            {
                query = query.Where(j => j.Joinedat <= filterParams.EndJoinedDate.Value);
            }

            if (!string.IsNullOrEmpty(filterParams.Search))
            {
                var searchLower = filterParams.Search.ToLower();
                query = query.Where(j => (j.User.Fullname != null && j.User.Fullname.ToLower().Contains(searchLower)) ||
                                        (j.Post.Courtbooking.Court.Owner.Fullname != null && j.Post.Courtbooking.Court.Owner.Fullname.ToLower().Contains(searchLower)) ||
                                        (j.Post.Courtbooking.Court.Location != null && j.Post.Courtbooking.Court.Location.ToLower().Contains(searchLower)));
            }

            var totalCount = await query.CountAsync();

            var sortBy = filterParams.SortBy?.ToLower() ?? "joinedat";
            var isDescending = filterParams.SortOrder?.ToLower() == "desc";

            query = sortBy switch
            {
                "username" => isDescending ? query.OrderByDescending(j => j.User.Fullname) : query.OrderBy(j => j.User.Fullname),
                "courtname" => isDescending ? query.OrderByDescending(j => j.Post.Courtbooking.Court.Owner.Fullname) : query.OrderBy(j => j.Post.Courtbooking.Court.Owner.Fullname),
                "courtlocation" => isDescending ? query.OrderByDescending(j => j.Post.Courtbooking.Court.Location) : query.OrderBy(j => j.Post.Courtbooking.Court.Location),
                "bookingdate" => isDescending ? query.OrderByDescending(j => j.Post.Courtbooking.Bookingdate) : query.OrderBy(j => j.Post.Courtbooking.Bookingdate),
                "slotstarttime" => isDescending ? query.OrderByDescending(j => j.Post.Courtbooking.Slot.Starttime) : query.OrderBy(j => j.Post.Courtbooking.Slot.Starttime),
                _ => isDescending ? query.OrderByDescending(j => j.Joinedat) : query.OrderBy(j => j.Joinedat)
            };

            // Apply pagination
            query = query
                .Skip((filterParams.Page - 1) * filterParams.PageSize)
                .Take(filterParams.PageSize);

            var joins = await query.ToListAsync();

            var responseData = joins.Select(join => new PlaymateJoinResponseDto
            {
                JoinId = join.JoinId,
                PostId = join.PostId,
                CourtbookingId = join.Post.CourtbookingId.Value,
                CourtId = join.Post.Courtbooking.CourtId,
                CourtName = join.Post.Courtbooking.Court.Owner.Fullname ?? "Unknown",
                CourtLocation = join.Post.Courtbooking.Court.Location ?? "Unknown",
                CourtImageUrls = join.Post.Courtbooking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                Bookingdate = join.Post.Courtbooking.Bookingdate,
                SlotStarttime = join.Post.Courtbooking.Slot.Starttime,
                SlotEndtime = join.Post.Courtbooking.Slot.Endtime,
                PostTitle = join.Post.Title,
                PostSkilllevel = join.Post.Skilllevel,
                PostStatus = join.Post.Status,
                Neededplayers = join.Post.Neededplayers,
                CurrentPlayers = join.Post.Playmatejoins?.Count ?? 0,
                UserId = join.UserId,
                UserName = join.User?.Fullname ?? "Unknown",
                Joinedat = join.Joinedat
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
                Message = "Your playmate joins retrieved successfully.",
                Pagination = paginationMetadata,
                Data = responseData
            });
        }

        [HttpGet("court/{courtId}")]
        [Authorize]
        public async Task<ActionResult> GetByCourt(int courtId, [FromQuery] PlaymateJoinFilterParams filterParams)
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

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == court.OwnerId;

            if (!isAdminOrStaff && !isOwner)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view playmate joins for this court." });
            }

            var query = _context.Playmatejoins
                .Include(j => j.User)
                .Include(j => j.Post)
                .ThenInclude(p => p.Courtbooking)
                .ThenInclude(b => b.Court)
                .ThenInclude(c => c.Owner)
                .Include(j => j.Post)
                .ThenInclude(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .Include(j => j.Post)
                .ThenInclude(p => p.Playmatejoins)
                .Where(j => j.Post.Courtbooking != null && j.Post.Courtbooking.CourtId == courtId && j.Post.Courtbooking.Status == "Confirmed" && j.Post.Courtbooking.Slot.Starttime > DateTime.UtcNow.AddHours(1))
                .AsQueryable();

            // Apply filters
            if (filterParams.PostId.HasValue)
            {
                query = query.Where(j => j.PostId == filterParams.PostId.Value);
            }

            if (filterParams.UserId.HasValue)
            {
                query = query.Where(j => j.UserId == filterParams.UserId.Value);
            }

            if (filterParams.StartJoinedDate.HasValue)
            {
                query = query.Where(j => j.Joinedat >= filterParams.StartJoinedDate.Value);
            }

            if (filterParams.EndJoinedDate.HasValue)
            {
                query = query.Where(j => j.Joinedat <= filterParams.EndJoinedDate.Value);
            }

            if (!string.IsNullOrEmpty(filterParams.Search))
            {
                var searchLower = filterParams.Search.ToLower();
                query = query.Where(j => (j.User.Fullname != null && j.User.Fullname.ToLower().Contains(searchLower)) ||
                                        (j.Post.Courtbooking.Court.Owner.Fullname != null && j.Post.Courtbooking.Court.Owner.Fullname.ToLower().Contains(searchLower)) ||
                                        (j.Post.Courtbooking.Court.Location != null && j.Post.Courtbooking.Court.Location.ToLower().Contains(searchLower)));
            }

            // Get total count for pagination
            var totalCount = await query.CountAsync();

            // Apply sorting
            var sortBy = filterParams.SortBy?.ToLower() ?? "joinedat";
            var isDescending = filterParams.SortOrder?.ToLower() == "desc";

            query = sortBy switch
            {
                "username" => isDescending ? query.OrderByDescending(j => j.User.Fullname) : query.OrderBy(j => j.User.Fullname),
                "courtname" => isDescending ? query.OrderByDescending(j => j.Post.Courtbooking.Court.Owner.Fullname) : query.OrderBy(j => j.Post.Courtbooking.Court.Owner.Fullname),
                "courtlocation" => isDescending ? query.OrderByDescending(j => j.Post.Courtbooking.Court.Location) : query.OrderBy(j => j.Post.Courtbooking.Court.Location),
                "bookingdate" => isDescending ? query.OrderByDescending(j => j.Post.Courtbooking.Bookingdate) : query.OrderBy(j => j.Post.Courtbooking.Bookingdate),
                "slotstarttime" => isDescending ? query.OrderByDescending(j => j.Post.Courtbooking.Slot.Starttime) : query.OrderBy(j => j.Post.Courtbooking.Slot.Starttime),
                _ => isDescending ? query.OrderByDescending(j => j.Joinedat) : query.OrderBy(j => j.Joinedat)
            };

            // Apply pagination
            query = query
                .Skip((filterParams.Page - 1) * filterParams.PageSize)
                .Take(filterParams.PageSize);

            var joins = await query.ToListAsync();

            var responseData = joins.Select(join => new PlaymateJoinResponseDto
            {
                JoinId = join.JoinId,
                PostId = join.PostId,
                CourtbookingId = join.Post.CourtbookingId.Value,
                CourtId = join.Post.Courtbooking.CourtId,
                CourtName = join.Post.Courtbooking.Court.Owner.Fullname ?? "Unknown",
                CourtLocation = join.Post.Courtbooking.Court.Location ?? "Unknown",
                CourtImageUrls = join.Post.Courtbooking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                Bookingdate = join.Post.Courtbooking.Bookingdate,
                SlotStarttime = join.Post.Courtbooking.Slot.Starttime,
                SlotEndtime = join.Post.Courtbooking.Slot.Endtime,
                PostTitle = join.Post.Title,
                PostSkilllevel = join.Post.Skilllevel,
                PostStatus = join.Post.Status,
                Neededplayers = join.Post.Neededplayers,
                CurrentPlayers = join.Post.Playmatejoins?.Count ?? 0,
                UserId = join.UserId,
                UserName = join.User?.Fullname ?? "Unknown",
                Joinedat = join.Joinedat
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
                Message = "Playmate joins for the court retrieved successfully.",
                Pagination = paginationMetadata,
                Data = responseData
            });
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> Delete(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid join ID." });
            }

            var join = await _context.Playmatejoins
                .Include(j => j.Post)
                .ThenInclude(p => p.Courtbooking)
                .ThenInclude(b => b.Court)
                .ThenInclude(c => c.Owner)
                .Include(j => j.Post)
                .ThenInclude(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .Include(j => j.Post)
                .ThenInclude(p => p.Playmatejoins)
                .FirstOrDefaultAsync(j => j.JoinId == id);

            if (join == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Playmate join not found." });
            }

            if (join.Post.Courtbooking == null || join.Post.Courtbooking.Status != "Confirmed" || join.Post.Courtbooking.Slot.Starttime <= DateTime.UtcNow.AddHours(1))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Playmate join is associated with an invalid or expired booking." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == join.UserId || currentUserId == join.Post.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to delete this playmate join." });
            }

            try
            {
                _context.Playmatejoins.Remove(join);

                if (join.Post.Status == "Closed" && join.Post.Playmatejoins.Count <= join.Post.Neededplayers)
                {
                    join.Post.Status = "Open";
                }

                await _context.SaveChangesAsync();

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Playmate join deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while deleting the playmate join: {ex.Message}" });
            }
        }
    }
}