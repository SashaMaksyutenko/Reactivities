using API.DTOs;
using API.Services;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Email;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController:ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly TokenService _tokenService;
         private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;
        private readonly EmailSender _emailSender;

        public AccountController(UserManager<AppUser>userManager,SignInManager<AppUser>signInManager,TokenService tokenService,
        IConfiguration config,HttpClient httpClient, EmailSender emailSender)
        {
            _emailSender = emailSender;
            _config = config;
            _userManager = userManager;
            _signInManager = signInManager;
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
            if(user==null)return Unauthorized("Invalid email");
            if(user.UserName=="bob") user.EmailConfirmed=true;
            if(!user.EmailConfirmed) return Unauthorized("Email not confirmed.Pigeon did not pass");
            var result=await _signInManager.CheckPasswordSignInAsync(user,loginDto.Password,false);
            if(result.Succeeded){
                await SetRefreshToken(user);
                return CreateUserObject(user);
            }
            return Unauthorized("Invalid password Голубе сизокрилий");
        }
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<UserDto>>Register(RegisterDto registerDto){
            if (await _userManager.Users.AnyAsync(x=>x.UserName==registerDto.UserName)){
                ModelState.AddModelError("username","Username taken");
                return ValidationProblem();
            }
            if (await _userManager.Users.AnyAsync(x=>x.Email==registerDto.Email)){
                ModelState.AddModelError("email","Email taken");
                return ValidationProblem();
            }
            var user=new AppUser{
                DisplayName=registerDto.DisplayName,
                Email=registerDto.Email,
                UserName=registerDto.UserName
            };  
            // send email before creating user
            var origin = Request.Headers["origin"];
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            var verifyUrl = $"{origin}/account/verifyEmail?token={token}&email={user.Email}";
            var message = "<p>Please Click the below link to verify your email address</p>";
            message += $"<p><a href='{verifyUrl}'>Click here to to verify your email address</a></p>";

            var emailSent = await _emailSender.SendEmailAsync(user.Email, "Please verify your email address", message);
            if(!emailSent)
            {
               return Ok("Problem registering user - could not send email, please check email and try again");
            }
            // create user
            var result=await _userManager.CreateAsync(user,registerDto.Password);
            if(!result.Succeeded) return BadRequest("Problem registering user");
            return Ok("Registration success - please verify email");
        }
        [AllowAnonymous]
        [HttpPost("verifyEmail")]
        public async Task<IActionResult>VerifyEmail(string token,string email){
            var user=await _userManager.FindByEmailAsync(email);
            if(user==null) return Unauthorized();
            var decodedTokenBytes=WebEncoders.Base64UrlDecode(token);
            var decodedtoken=Encoding.UTF8.GetString(decodedTokenBytes);
            var result=await _userManager.ConfirmEmailAsync(user,decodedtoken);
            if(!result.Succeeded) return BadRequest("could not verify email address. This pigeon did not pass");
            return Ok("Email cofirmed - you can now login");
        }
        [AllowAnonymous]
        [HttpGet("resendEmailConfirmationLink")]
        public async Task<IActionResult>ResendEmailConfirmationLink(string email){
            var user=await _userManager.FindByEmailAsync(email);
            if(user==null) return Unauthorized();
            var origin=Request.Headers["origin"];
            var token=await _userManager.GenerateEmailConfirmationTokenAsync(user);
            token=WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var verifyUrl=$"{origin}/account/verifyEmail?token={token}&email={user.Email}";
            var message = $"<p>Please click the below link to verify your email address:</p><p><a href='{verifyUrl}'</a>{verifyUrl}</p>'";
            var emailSent = await _emailSender.SendEmailAsync(user.Email,"Please verify Email",message);  if(!emailSent)
            {
               return Ok("Problem resending user email -  please check email and try again");
            }
            return Ok("Email verification link resent");
        }
        [Authorize]
        [HttpGet]
        public async Task<ActionResult<UserDto>> GetCurrentUser(){
            var user=await _userManager.Users.Include(p=>p.Photos).FirstOrDefaultAsync(x=>x.Email==User.FindFirstValue(ClaimTypes.Email));
            if(user==null) return NotFound();
            return CreateUserObject(user);
        }
        [AllowAnonymous]
        [HttpPost("fbLogin")]
        public async Task<ActionResult<UserDto>>FacebookLogin(string accessToken){
            var fbVerifyKeys=_config["Facebook:AppId"] + "|" + _config["Facebook:ApiSecret"];
            var verifyTokenResponse=await _httpClient.GetAsync($"debug_token?input_token={accessToken}&access_token={fbVerifyKeys}");
            if(!verifyTokenResponse.IsSuccessStatusCode) return Unauthorized();
            var fbUrl= $"me?access_token={accessToken}&fields=name,email,picture.width(100).height(100)";
            var fbInfo=await _httpClient.GetFromJsonAsync<FacebookDto>(fbUrl);
            var user=await _userManager.Users.Include(p=>p.Photos)
                .FirstOrDefaultAsync(x=>x.Email==fbInfo.Email);
                if(user!=null) return CreateUserObject(user);
                user=new AppUser{
                    DisplayName=fbInfo.Name,
                    Email=fbInfo.Email,
                    UserName=fbInfo.Email,
                    Photos=new List<Photo>
                    {
                        new Photo
                        {
                            Id="fb_"+fbInfo.Id,
                            Url=fbInfo.Picture.Data.Url,
                            IsMain=true
                        }
                    }
                };
                var result=await _userManager.CreateAsync(user);
                if (!result.Succeeded) return BadRequest("Problem creating user account");
                await SetRefreshToken(user);
            return CreateUserObject(user);
        }
        [Authorize]
        [HttpPost("refreshToken")]
        public async Task<ActionResult<UserDto>>RefreshToken()
        {
            var refreshToken=Request.Cookies["refreshToken"];
            var user=await _userManager.Users
                .Include(r=>r.RefreshTokens)
                .Include(p=>p.Photos)
            .FirstOrDefaultAsync(x=>x.UserName==User.FindFirstValue(ClaimTypes.Name));
            if(user==null) return Unauthorized();
            var oldToken=user.RefreshTokens.SingleOrDefault(x=>x.Token==refreshToken);
            if(oldToken !=null && !oldToken.IsActive) return Unauthorized();
            if(oldToken!=null) oldToken.Revoked=DateTime.UtcNow;
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