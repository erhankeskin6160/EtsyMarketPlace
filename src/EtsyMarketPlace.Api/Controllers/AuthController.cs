using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Application.Auth;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepo;
    private readonly JwtTokenService _jwtService;

    public AuthController(IUserRepository userRepo, JwtTokenService jwtService)
    {
        _userRepo = userRepo;
        _jwtService = jwtService;
    }

    [HttpPost("login")]
    [EndpointSummary("Kullanıcı Girişi")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new AuthResponse(false, null, "Kullanıcı adı/e-posta ve şifre gereklidir.", null));
        }

        var user = await _userRepo.GetByUsernameOrEmailAsync(request.UsernameOrEmail, cancellationToken);
        if (user == null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new AuthResponse(false, null, "Geçersiz kullanıcı adı veya şifre.", null));
        }

        if (!user.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new AuthResponse(false, null, "Hesabınız askıya alınmıştır. Lütfen yöneticiyle iletişime geçin.", null));
        }

        var now = DateTimeOffset.UtcNow;
        await _userRepo.UpdateLastLoginAsync(user.Id, now, cancellationToken);
        await _userRepo.AddAuditLogAsync(new AuditLogEntry(0, user.Id, user.Username, "Login", "Giriş başarılı", HttpContext.Connection.RemoteIpAddress?.ToString(), now), cancellationToken);

        var token = _jwtService.GenerateToken(user);
        var userDto = new UserDto(user.Id, user.Username, user.Email, user.Role, user.AssignedShopIds, user.MonthlyAiTokenQuota, user.UsedAiTokens, user.IsActive, user.CreatedAt.ToString("O"), now.ToString("O"));

        return Ok(new AuthResponse(true, token, "Giriş başarılı.", userDto));
    }

    [HttpPost("register")]
    [EndpointSummary("Yeni Kullanıcı Kaydı")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
        {
            return BadRequest(new AuthResponse(false, null, "Kullanıcı adı en az 3 karakter olmalıdır.", null));
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            return BadRequest(new AuthResponse(false, null, "Geçerli bir e-posta adresi giriniz.", null));
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return BadRequest(new AuthResponse(false, null, "Şifre en az 6 karakter olmalıdır.", null));
        }

        var existingUser = await _userRepo.GetByUsernameOrEmailAsync(request.Username, cancellationToken)
            ?? await _userRepo.GetByUsernameOrEmailAsync(request.Email, cancellationToken);

        if (existingUser != null)
        {
            return Conflict(new AuthResponse(false, null, "Bu kullanıcı adı veya e-posta zaten kullanımda.", null));
        }

        var (hash, salt) = PasswordHasher.HashPassword(request.Password);
        var now = DateTimeOffset.UtcNow;
        var assignedShops = string.IsNullOrWhiteSpace(request.ShopId) ? new List<string> { "53236321" } : new List<string> { request.ShopId.Trim() };

        var newUser = new AppUser(
            Guid.NewGuid().ToString(),
            request.Username.Trim(),
            request.Email.Trim().ToLowerInvariant(),
            hash,
            salt,
            UserRoles.StoreOwner,
            assignedShops,
            500_000,
            0,
            true,
            now,
            now);

        await _userRepo.CreateUserAsync(newUser, cancellationToken);
        await _userRepo.AddAuditLogAsync(new AuditLogEntry(0, newUser.Id, newUser.Username, "Register", "Yeni kayıt oluşturuldu", HttpContext.Connection.RemoteIpAddress?.ToString(), now), cancellationToken);

        var token = _jwtService.GenerateToken(newUser);
        var userDto = new UserDto(newUser.Id, newUser.Username, newUser.Email, newUser.Role, newUser.AssignedShopIds, newUser.MonthlyAiTokenQuota, newUser.UsedAiTokens, newUser.IsActive, newUser.CreatedAt.ToString("O"), now.ToString("O"));

        return Ok(new AuthResponse(true, token, "Kayıt başarıyla tamamlandı.", userDto));
    }

    [HttpGet("me")]
    [EndpointSummary("Geçerli Kullanıcı Bilgileri")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var authHeader = HttpContext.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized();
        }

        var token = authHeader["Bearer ".Length..].Trim();
        var (isValid, _, userId, _, _) = _jwtService.ValidateToken(token);
        if (!isValid || string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _userRepo.GetByIdAsync(userId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var userDto = new UserDto(user.Id, user.Username, user.Email, user.Role, user.AssignedShopIds, user.MonthlyAiTokenQuota, user.UsedAiTokens, user.IsActive, user.CreatedAt.ToString("O"), user.LastLoginAt?.ToString("O"));
        return Ok(userDto);
    }
}
