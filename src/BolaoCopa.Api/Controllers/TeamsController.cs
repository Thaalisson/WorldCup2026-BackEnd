using BolaoCopa.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BolaoCopa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TeamsController : ControllerBase
{
    private readonly ITeamRepository _teams;

    public TeamsController(ITeamRepository teams) => _teams = teams;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var teams = await _teams.GetAllAsync();
        return Ok(teams.Select(t => new
        {
            t.Id,
            t.Name,
            t.Code,
            t.IsoCode,
            t.GroupName
        }));
    }
}
