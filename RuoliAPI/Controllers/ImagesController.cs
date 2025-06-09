using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using RuoliAPI.Models;
using System.Security.Claims;

namespace RuoliAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImagesController : Controller
    {
        private readonly CalligraphyContext _context;

        public ImagesController(CalligraphyContext context)
        {
            _context = context;
        }

        //GET api/images
        [HttpGet(Name = "GetImage")]
        public async Task<IActionResult> GetImage()
        {
            //讀取所有isVisivble是true的圖片
            var imgs = await _context.TbExhArtwork
                .AsNoTracking()
                .Where(i => i.IsVisible)
                .Select(i => new
                {
                    i.ArtworkId,
                    i.Title,
                    i.CreatedYear,
                    i.ImageUrl,
                    i.Description,
                    i.Dimensions,
                    i.Material
                }).OrderByDescending(i => i.CreatedYear)
                .ToListAsync();

            if (imgs == null || imgs.Count == 0)
            {
                return NotFound("No images found.");
            }
            return Ok(imgs);
        }

        //回傳該作品的讚數
        [HttpGet("{artworkId}/likes", Name = "GetArtworkLikes")]
        public async Task<IActionResult> GetArtworkLikes(Guid artworkId)
        {
            var likeCount = await _context.TbExhLike
                .AsNoTracking()
                .CountAsync(l => l.ArtworkId == artworkId);
            return Ok(new { ArtworkId = artworkId, LikeCount = likeCount });
        }

        //新增讚數
        [HttpPost("{artworkId}/likes", Name = "AddArtworkLike")]
        public async Task<IActionResult> AddArtworkLike(Guid artworkId)
        {
            var artwork = await _context.TbExhArtwork.FindAsync(artworkId);
            if (artwork == null)
            {
                return NotFound("Artwork not found.");
            }

            //同一個ip一天只能按一次讚
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var existingLike = await _context.TbExhLike
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.ArtworkId == artworkId && l.IpAddress == ip && l.CreateDate.Date == DateTimeOffset.Now.Date);
            if (existingLike == null)
            {
                var like = new TbExhLike
                {
                    ArtworkId = artworkId,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                    CreateFrom = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                    Creator = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
                };
                _context.TbExhLike.Add(like);
                try
                {
                    await _context.SaveChangesAsync();
                    return Ok();
                }
                catch (DbUpdateException ex)
                {
                    //處理資料庫更新異常
                    return BadRequest($"Error adding like: {ex.Message}");
                }
            }
            else 
            {                 
                return Ok("今日已按過讚");
            }
        }
    }
}
