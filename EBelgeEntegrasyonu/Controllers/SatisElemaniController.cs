using EBelgeAPI.Models.Responses;
using EBelgeAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EBelgeAPI.Controllers;

[ApiController]
[Route("api/satis-elemani")]
[Authorize]
public class SatisElemaniController(
    ISatisElemaniService satisElemaniService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        try
        {
            var list = await satisElemaniService.GetListAsync();
            return Ok(ApiResponse<object>.Ok(list));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
    [HttpDelete("cache")]
    public IActionResult ClearCache(
        [FromServices] Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        cache.Remove("satis_elemanlari");
        return Ok(ApiResponse<object>.Ok(null));
    }
}