using GetSportAPI.Models.Generated;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GetSportAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HealthServerCheckController : ControllerBase
    {
        [HttpGet("ping")]
        public ActionResult Ping()
        {
            return Ok(new
            {
                statusCode = 200,
                status = "Success",
                message = "Service is running. Welcome to Get Sport API.",
                timestamp = DateTime.UtcNow
            });
        }

        [HttpGet("check-db")]
        public async Task<ActionResult> CheckDatabase([FromServices] GetSportContext context)
        {
            try
            {
                var canConnect = await context.Database.CanConnectAsync();

                if (!canConnect)
                {
                    return StatusCode(500, new
                    {
                        statusCode = 500,
                        status = "Error",
                        message = "Cannot connect to database."
                    });
                }

                return Ok(new
                {
                    statusCode = 200,
                    status = "Success",
                    message = "Database connection is healthy."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    statusCode = 500,
                    status = "Error",
                    message = "Database check failed.",
                    error = ex.Message
                });
            }
        }
    }
}
