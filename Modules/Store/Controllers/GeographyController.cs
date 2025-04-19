using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AdminApi.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Store.Interface;
using Store.Models;
using Store.Models.Dtos;
using Users.Models;

namespace Store.Controllers
{
    [Authorize(Roles = "Admin, Seller, Buyer")]
    [ApiController]
    [Route("api/[controller]")]
    public class GeographyController : ControllerBase
    {
        private readonly ILogger<GeographyController> _logger;
        private readonly IGeographyService _geographyService;
        public GeographyController(
                    IGeographyService _geographyService,
                    ILogger<GeographyController> logger)
        {
            _geographyService = _geographyService;
            _logger = logger;
        }
        [HttpGet("geography")] // GET /api/catalog/categories/5
        [ProducesResponseType(typeof(GeographyGetDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetRegions()
        {
            var regions = await _geographyService.GetAllRegionsAsync();
            var places = await _geographyService.GetAllPlacesAsync();

            return Ok(new GeographyGetDto
            {
                Regions = regions.Select(r => new RegionDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    CountryCode = r.Country
                }).ToList(),
                Places = places.Select(p => new PlaceDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    PostalCode = p.PostalCode
                }).ToList(),
            });
        }

    }
}