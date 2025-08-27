using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GetSportAPI.Models.Generated;
using GetSportAPI.DTO;
using GetSportAPI.Models.Enum;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using System.ComponentModel.DataAnnotations;
using GetSportAPI.Utils;

namespace GetSportAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BlogController : ControllerBase
    {
        private readonly GetSportContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly string[] _validStatuses = { BlogStatus.Draft, BlogStatus.Published, BlogStatus.Banned };
        private readonly string[] _validCreateStatuses = { BlogStatus.Draft, BlogStatus.Published };

        public BlogController(GetSportContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult> Create([FromForm] BlogCreateDto dto)
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
            if (userIdClaim == null)
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            int accountId;
            if (!int.TryParse(userIdClaim, out accountId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "Invalid user ID." });
            }

            try
            {
                ImageService imageService = new ImageService(_environment);
                string? imageUrl = await imageService.SaveImageAsync(dto.Image);

                var blog = new Blogpost
                {
                    AccountId = accountId,
                    Title = dto.Title.Trim(),
                    Shortdesc = dto.Shortdesc.Trim(),
                    Content = dto.Content.Trim(),
                    Imageurl = imageUrl,
                    Status = BlogStatus.Draft,
                    Createdat = DateTime.UtcNow
                };

                _context.Blogposts.Add(blog);
                await _context.SaveChangesAsync();

                var account = await _context.Accounts.FindAsync(accountId);
                var responseData = new BlogResponseDto
                {
                    BlogId = blog.BlogId,
                    Title = blog.Title,
                    Shortdesc = blog.Shortdesc,
                    Content = blog.Content,
                    Imageurl = blog.Imageurl,
                    Status = blog.Status,
                    Createdat = blog.Createdat,
                    Updatedat = blog.Updatedat,
                    AccountId = blog.AccountId,
                    AuthorName = account?.Fullname ?? "Unknown"
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Blog post created successfully.", Data = responseData });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while creating the blog post: {ex.Message}" });
            }
        }

        [HttpPost("create-with-status")]
        [Authorize]
        public async Task<ActionResult> CreateWithStatus([FromForm] BlogCreateWithStatusDto dto)
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
            if (userIdClaim == null)
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            int accountId;
            if (!int.TryParse(userIdClaim, out accountId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "Invalid user ID." });
            }

            if (!_validCreateStatuses.Contains(dto.Status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Draft, Published." });
            }

            try
            {
                ImageService imageService = new ImageService(_environment);
                string? imageUrl = await imageService.SaveImageAsync(dto.Image);

                var blog = new Blogpost
                {
                    AccountId = accountId,
                    Title = dto.Title.Trim(),
                    Shortdesc = dto.Shortdesc.Trim(),
                    Content = dto.Content.Trim(),
                    Imageurl = imageUrl,
                    Status = dto.Status.Trim(),
                    Createdat = DateTime.UtcNow
                };

                _context.Blogposts.Add(blog);
                await _context.SaveChangesAsync();

                var account = await _context.Accounts.FindAsync(accountId);
                var responseData = new BlogResponseDto
                {
                    BlogId = blog.BlogId,
                    Title = blog.Title,
                    Shortdesc = blog.Shortdesc,
                    Content = blog.Content,
                    Imageurl = blog.Imageurl,
                    Status = blog.Status,
                    Createdat = blog.Createdat,
                    Updatedat = blog.Updatedat,
                    AccountId = blog.AccountId,
                    AuthorName = account?.Fullname ?? "Unknown"
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Blog post created successfully.", Data = responseData });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while creating the blog post: {ex.Message}" });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult> GetById(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid blog ID." });
            }

            var blog = await _context.Blogposts
                .Include(b => b.Account)
                .FirstOrDefaultAsync(b => b.BlogId == id && b.Status != BlogStatus.Deleted);

            if (blog == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Blog post not found or has been deleted." });
            }

            bool isAuthenticated = User.Identity?.IsAuthenticated ?? false;
            if (blog.Status == BlogStatus.Draft)
            {
                if (!isAuthenticated)
                {
                    return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this draft post." });
                }
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim, out int accountId) || (blog.AccountId != accountId && User.FindFirstValue(ClaimTypes.Role) != UserRole.Admin))
                {
                    return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this draft post." });
                }
            }

            if (blog.Status == BlogStatus.Banned)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "This blog post is banned and cannot be viewed." });
            }

            var responseData = new BlogResponseDto
            {
                BlogId = blog.BlogId,
                Title = blog.Title,
                Shortdesc = blog.Shortdesc,
                Content = blog.Content,
                Imageurl = blog.Imageurl,
                Status = blog.Status,
                Createdat = blog.Createdat,
                Updatedat = blog.Updatedat,
                AccountId = blog.AccountId,
                AuthorName = blog.Account?.Fullname ?? "Unknown"
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Blog post retrieved successfully.", Data = responseData });
        }

        [HttpGet]
        public async Task<ActionResult> GetAll([FromQuery] string? status = null)
        {
            if (!string.IsNullOrEmpty(status) && !_validStatuses.Contains(status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Draft, Published, Banned." });
            }

            bool isAuthenticated = User.Identity?.IsAuthenticated ?? false;
            int? accountId = null;
            string? userRole = null;
            if (isAuthenticated)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim, out int parsedId))
                {
                    accountId = parsedId;
                }
                userRole = User.FindFirstValue(ClaimTypes.Role);
            }

            var isAdmin = userRole == UserRole.Admin;

            var query = _context.Blogposts
                .Include(b => b.Account)
                .Where(b => b.Status != BlogStatus.Deleted)
                .AsQueryable();

            if (!isAuthenticated)
            {
                // Guests can only see Published blog posts
                query = query.Where(b => b.Status == BlogStatus.Published);
            }
            else
            {
                // Authenticated users
                if (!isAdmin && accountId.HasValue)
                {
                    query = query.Where(b => b.Status == BlogStatus.Published || b.Status == BlogStatus.Banned || (b.Status == BlogStatus.Draft && b.AccountId == accountId));
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(b => b.Status == status);
                }
            }

            var blogs = await query.ToListAsync();

            var responseData = blogs.Select(blog => new BlogResponseDto
            {
                BlogId = blog.BlogId,
                Title = blog.Title,
                Shortdesc = blog.Shortdesc,
                Content = blog.Content,
                Imageurl = blog.Imageurl,
                Status = blog.Status,
                Createdat = blog.Createdat,
                Updatedat = blog.Updatedat,
                AccountId = blog.AccountId,
                AuthorName = blog.Account?.Fullname ?? "Unknown"
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Blog posts retrieved successfully.", Data = responseData });
        }

        [HttpGet("my")]
        [Authorize]
        public async Task<ActionResult> GetMyPosts()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            int accountId;
            if (!int.TryParse(userIdClaim, out accountId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "Invalid user ID." });
            }

            var blogs = await _context.Blogposts
                .Include(b => b.Account)
                .Where(b => b.AccountId == accountId && b.Status != BlogStatus.Deleted)
                .ToListAsync();

            var responseData = blogs.Select(blog => new BlogResponseDto
            {
                BlogId = blog.BlogId,
                Title = blog.Title,
                Shortdesc = blog.Shortdesc,
                Content = blog.Content,
                Imageurl = blog.Imageurl,
                Status = blog.Status,
                Createdat = blog.Createdat,
                Updatedat = blog.Updatedat,
                AccountId = blog.AccountId,
                AuthorName = blog.Account?.Fullname ?? "Unknown"
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "My blog posts retrieved successfully.", Data = responseData });
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult> Update(int id, [FromForm] BlogUpdateDto dto)
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
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid blog ID." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            int accountId;
            if (!int.TryParse(userIdClaim, out accountId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "Invalid user ID." });
            }

            var blog = await _context.Blogposts
                .Include(b => b.Account)
                .FirstOrDefaultAsync(b => b.BlogId == id && b.Status != BlogStatus.Deleted);

            if (blog == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Blog post not found or has been deleted." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            if (blog.AccountId != accountId && userRole != UserRole.Admin)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to update this blog post." });
            }

            try
            {
                if (!string.IsNullOrEmpty(dto.Title)) blog.Title = dto.Title.Trim();
                if (!string.IsNullOrEmpty(dto.Shortdesc)) blog.Shortdesc = dto.Shortdesc.Trim();
                if (!string.IsNullOrEmpty(dto.Content)) blog.Content = dto.Content.Trim();
                if (dto.Image != null)
                {
                    ImageService imageService = new ImageService(_environment);
                    string? newImageUrl = await imageService.SaveImageAsync(dto.Image);
                    if (newImageUrl != null) blog.Imageurl = newImageUrl;
                }
                if (!string.IsNullOrEmpty(dto.Status) && userRole == UserRole.Admin)
                {
                    if (!_validStatuses.Contains(dto.Status))
                    {
                        return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Draft, Published, Banned." });
                    }
                    blog.Status = dto.Status.Trim();
                }

                blog.Updatedat = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var responseData = new BlogResponseDto
                {
                    BlogId = blog.BlogId,
                    Title = blog.Title,
                    Shortdesc = blog.Shortdesc,
                    Content = blog.Content,
                    Imageurl = blog.Imageurl,
                    Status = blog.Status,
                    Createdat = blog.Createdat,
                    Updatedat = blog.Updatedat,
                    AccountId = blog.AccountId,
                    AuthorName = blog.Account?.Fullname ?? "Unknown"
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Blog post updated successfully.", Data = responseData });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while updating the blog post: {ex.Message}" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> Delete(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid blog ID." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            int accountId;
            if (!int.TryParse(userIdClaim, out accountId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "Invalid user ID." });
            }

            var blog = await _context.Blogposts
                .FirstOrDefaultAsync(b => b.BlogId == id && b.Status != BlogStatus.Deleted);

            if (blog == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Blog post not found or has been deleted." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            if (blog.AccountId != accountId && userRole != UserRole.Admin)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to delete this blog post." });
            }

            try
            {
                blog.Status = BlogStatus.Deleted;
                blog.Updatedat = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Blog post marked as deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while deleting the blog post: {ex.Message}" });
            }
        }
    }
}