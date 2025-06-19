using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using RuoliAPI.Models;
using System.Security.Claims;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Calligraphy.Services.Interfaces;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace RuoliAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImagesController : Controller
    {
        private readonly CalligraphyContext _context;
        private readonly string _channelAccessToken;
        private readonly string _lineUser;
        private readonly IClientIpService _getIP;

        public ImagesController(CalligraphyContext context, IConfiguration configuration, IClientIpService getIP)
        {
            _context = context;
            _channelAccessToken = configuration["LineToken:ChannelAccessToken"] ?? string.Empty;
            _lineUser = configuration["LineToken:userId"] ?? string.Empty;
            _getIP = getIP;
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
                    i.Material,
                    i.Views
                }).OrderByDescending(i => i.CreatedYear)
                .ToListAsync();

            if (imgs == null || imgs.Count == 0)
            {
                return NotFound("No images found.");
            }
            return Ok(imgs);
        }

        //PUT瀏覽人次
        [HttpPut(Name = "AddArtworkView")]
        public async Task<IActionResult> AddArtworkView([FromBody] Guid artworkId)
        {
            var artwork = await _context.TbExhArtwork.FindAsync(artworkId);
            if (artwork == null)
            {
                return NotFound("Artwork not found.");
            }
            var writer = await _context.TbExhUser
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == artwork.Writer);
            artwork.Views = (artwork.Views ?? 0) + 1;
            artwork.ModifyFrom = _getIP.GetClientIP() ?? null;
            try
            {
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (DbUpdateException ex)
            {
                //處理資料庫更新異常
                return BadRequest($"Error adding view: {ex.Message}");
            }
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
            isRock.LineBot.Bot bot = new isRock.LineBot.Bot(_channelAccessToken);
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
                    bot.PushMessage(_lineUser, new Uri($"https://ruolibackend.com/{artwork.ImageUrl}"));
                    bot.PushMessage(_lineUser, $"您的作品《{artwork.Title}》獲得了一個新的讚 !");
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
