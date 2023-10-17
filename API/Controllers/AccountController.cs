using API.DTOs;
using API.Services;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController:ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly TokenService _tokenService;
         private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public AccountController(UserManager<AppUser>userManager,TokenService tokenService,IConfiguration config,HttpClient httpClient)
        {
            _config = config;
            _userManager = userManager;
            _tokenService = tokenService;
            _httpClient=new HttpClient{
                BaseAddress=new System.Uri("https://graph.facebook.com")
            };
        }
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login(LoginDto loginDto){
            var user=await _userManager.Users.Include(p=>p.Photos)
                .FirstOrDefaultAsync(x=>x.Email==loginDto.Email);
            if(user==null)return Unauthorized();
            var result=await _userManager.CheckPasswordAsync(user,loginDto.Password);
            if(result){
                await SetRefreshToken(user);
                return CreateUserObject(user);
            }
            return Unauthorized();
        }
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<UserDto>>Register(RegisterDto registerDto){
            if (await _userManager.Users.AnyAsync(x=>x.Email==registerDto.Email)){
                ModelState.AddModelError("email","Email taken");
                return ValidationProblem();
            }
            if (await _userManager.Users.AnyAsync(x=>x.UserName==registerDto.UserName)){
                ModelState.AddModelError("username","Username taken");
                return ValidationProblem();
            }
            var user=new AppUser{
                DisplayName=registerDto.DisplayName,
                Email=registerDto.Email,
                UserName=registerDto.UserName
            };
            var result=await _userManager.CreateAsync(user,registerDto.Password);
            if(result.Succeeded)
            {
                await SetRefreshToken(user);
                return CreateUserObject(user);
            }
            return BadRequest(result.Errors);
        }
        [Authorize]
        [HttpGet]
        public async Task<ActionResult<UserDto>> GetCurrentUser(){
            var user=await _userManager.Users.Include(p=>p.Photos).FirstOrDefaultAsync(x=>x.Email==User.FindFirstValue(ClaimTypes.Email));
            if(user==null) return NotFound();
            return CreateUserObject(user);
        }
        private async Task SetRefreshToken(AppUser user){
            var refreshToken=_tokenService.GenerateRefreshToken();
            user.RefreshTokens.Add(refreshToken);
            await _userManager.UpdateAsync(user);
            var cookieOptions=new CookieOptions{
                HttpOnly=true,
                Expires=DateTime.UtcNow.AddDays(7)
            };
            Response.Cookies.Append("refreshToken",refreshToken.Token,cookieOptions);
        }
        public UserDto CreateUserObject (AppUser user)
        {
            return new UserDto
            {
                DisplayName = user.DisplayName,
                Image = user?.Photos?.FirstOrDefault(x=>x.IsMain)?.Url,
                Token = _tokenService.CreateToken(user),
                UserName = user.UserName,
            };
        }
    }
}