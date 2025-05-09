// Example: MappingProfile.cs (place where DI can find it)
using AutoMapper;
using MarketingAnalytics.Dtos;
using MarketingAnalytics.DTOs;
using MarketingAnalytics.Models;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Map from domain model to DTO
        CreateMap<Advertisment, AdvertismentDto>(); // AutoMapper handles the nested AdData collection mapping automatically
        CreateMap<AdData, AdDataDto>();

        // Add .ReverseMap() if you need mapping from DTO back to domain model elsewhere
        // CreateMap<Advertisment, AdvertismentDto>().ReverseMap();
        // CreateMap<AdData, AdDataDto>().ReverseMap();
    }
}