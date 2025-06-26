using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using RuoliAPI.Models;
using System.Security.Claims;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using RuoliAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Calligraphy.Services;

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
        private readonly ILogService _log;

        public ImagesController(CalligraphyContext context, IConfiguration configuration, IClientIpService getIP, ILogService log)
        {
            _context = context;
            _channelAccessToken = configuration["LineToken:ChannelAccessToken"] ?? string.Empty;
            _lineUser = configuration["LineToken:userId"] ?? string.Empty;
            _getIP = getIP;
            _log = log;
        }

        //GET api/images
        [HttpGet(Name = "GetImage")]
        public async Task<IActionResult> GetImage([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1)
            {
                pageNumber = 1;
            }
            if (pageSize < 1)
            {
                pageSize = 10;
            }

            // 讀取所有 isVisible 是 true 的圖片
            var query = _context.TbExhArtwork
                .AsNoTracking()
                .Where(i => i.IsVisible);

            var totalCount = await query.CountAsync();

            var imgs = await query
                .OrderByDescending(i => i.CreatedYear)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
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
                })
                .ToListAsync();

            if (imgs == null || imgs.Count == 0)
            {
                return NotFound("No images found for the specified page.");
            }

            var paginatedResult = new PaginatedResult<object>(imgs, totalCount, pageNumber, pageSize);
            return Ok(paginatedResult);
        }

        //POST瀏覽人次
        [HttpPost("{artworkId}/views", Name = "AddArtworkView")]
        public async Task<IActionResult> AddArtworkView([FromRoute] Guid artworkId)
        {
            var artwork = await _context.TbExhArtwork.FindAsync(artworkId);
            if (artwork == null)
            {
                return NotFound("Artwork not found.");
            }
            artwork.Views = artwork.Views + 1;
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

        //取得所有作品讚數 回傳json
        [HttpGet("likes", Name = "GetAllArtworkLikes")]
        public async Task<IActionResult> GetAllArtworkLikes()
        {
            var artworkLikes = await _context.TbExhLike
                .AsNoTracking()
                .GroupBy(l => l.ArtworkId)
                .Select(g => new
                {
                    ArtworkId = g.Key,
                    LikeCount = g.Count()
                })
                .ToListAsync();
            return Ok(artworkLikes);
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
            var ip = _getIP.GetClientIP() ?? "";
            var today = TimeHelper.GetTaipeiTimeNowOffset(DateTimeOffset.Now).Date;
            var existingLike = await _context.TbExhLike
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.ArtworkId == artworkId && l.IpAddress == ip && l.CreateDate.Date == today);
            if (existingLike == null)
            {
                var like = new TbExhLike
                {
                    ArtworkId = artworkId,
                    IpAddress = _getIP.GetClientIP() ?? "",
                    CreateFrom = _getIP.GetClientIP() ?? "",
                    Creator = _getIP.GetClientIP() ?? ""
                };
                _context.TbExhLike.Add(like);
                try
                {
                    await _context.SaveChangesAsync();
                    await _log.LogAsync(Guid.Empty, "AddLike", $"作品: {artwork.Title}, 來自 {ip} 的讚", _getIP.GetClientIP() ?? "");
                    bot.PushMessage(_lineUser, new Uri($"https://ruolibackend.com{artwork.ImageUrl}"));
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
