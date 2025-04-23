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
                    IGeographyService geographyService,
                    ILogger<GeographyController> logger)
        {
            _geographyService = geographyService;
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

        [HttpGet("regions")] // GET /api/Geography/regions
        [ProducesResponseType(typeof(List<RegionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetOnlyRegions()
        {
            var regions = await _geographyService.GetAllRegionsAsync();

            return Ok(regions.Select(r => new RegionDto
            {
                Id = r.Id,
                Name = r.Name,
                CountryCode = r.Country
            }).ToList());
        }

        [HttpGet("region/{id}")] // GET /api/Geography/region/5
        [ProducesResponseType(typeof(List<PlaceDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetRegionPlaces(int id)
        {
            var places = await _geographyService.GetAllPlacesInRegionAsync(id);

            return Ok(places.Select(p => new PlaceDto
            {
                Id = p.Id,
                Name = p.Name,
                PostalCode = p.PostalCode
            }).ToList());
        }
    }
}