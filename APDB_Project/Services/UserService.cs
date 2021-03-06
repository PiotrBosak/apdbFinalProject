﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using APDB_Project.Domain;
using APDB_Project.Dtos;
using APDB_Project.Exceptions;
using APDB_Project.Utilities;
using Castle.Core;
using Castle.Core.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using static APDB_Project.Utilities.SecurityUtility;

namespace APDB_Project.Services
{
    public class UserService : IUserService

    {
        private readonly AdvertisementContext _context;
        private readonly IConfiguration _configuration;
        private readonly IDividingService _dividingDividingService;
        private readonly IBannerService _bannerService;
        private static readonly Regex Regex1 = new Regex("\\d{9}");
        private static readonly Regex Regex2 = new Regex("\\d{3}-\\d{3}-\\d{3}");

        public UserService(AdvertisementContext context, IConfiguration configuration
            , IDividingService dividingService, IBannerService bannerService)
        {
            _configuration = configuration;
            _context = context;
            _dividingDividingService = dividingService;
            _bannerService = bannerService;
        }


        public ICollection<CampaignDto> ListCampaigns()
        {
            var list = new List<CampaignDto>();
            var campaigns = _context.Campaigns.Include(c => c.Banners)
                .Include(c => c.Client);

            foreach (var campaign in campaigns)
            {
                list.Add(new CampaignDto
                {
                    Client = GetClientInformation(campaign.Client),
                    Banners = GetBannersList(campaign.Banners),
                    EndDate = campaign.EndDate,
                    StartDate = campaign.StartDate,
                    FromBuilding = campaign.FromBuilding,
                    ToBuilding = campaign.ToBuilding,
                    PricePerSquareMeter = campaign.PricePerSquareMeter
                });
            }

            list.Sort((dto1, dto2) => (dto1.StartDate.CompareTo(dto2.StartDate)));
            return list;

        }


        private static ClientDto GetClientInformation(Client client)
        {
            return new ClientDto
            {
                Email = client.Email,
                Login = client.Login,
                FirstName = client.FirstName,
                LastName = client.LastName,
                PhoneNumber = client.PhoneNumber
            };
        }

        private static List<BannerDto> GetBannersList(List<Banner> banners)
        {
            var collection = new List<BannerDto>();
            foreach (var banner in banners)
            {
                collection.Add(new BannerDto
                {
                    Area = banner.Area,
                    Name = banner.Name,
                    Price = banner.Price
                });
            }

            return collection;
        }

        public JwtSecurityToken RegisterUser(UserRegistrationDto dto)
        {
            if (IsRegistrationDtoValid(dto))
            {
                if (IsLoginTaken(dto.Login))
                    throw new LoginAlreadyTakenException();

                var securePassword = SecurePassword(dto.Password);
                _context.Clients.Add(new Client
                {
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    Campaigns = new List<Campaign>(),
                    Email = dto.Email,
                    Login = dto.Login,
                    PhoneNumber = dto.Phone,
                    Password = securePassword
                });
                _context.SaveChanges();
                return CreateToken();

            }

            else throw new InvalidRegistrationDataException();
        }

        public JwtSecurityToken GetNewAccessToken(Token token)
        {
            if (IsTokenValid(token))
                return CreateToken();
            else throw new InvalidTokenException();
        }

        private bool IsTokenValid(Token token)
        {
            return _context
                .Tokens
                .Any(t => t.AccessToken == token.AccessToken && t.RefreshToken == token.RefreshToken);
        }

        public JwtSecurityToken LoginUser(UserLoginDto dto)
        {
            var client = _context.Clients.FirstOrDefault(c => c.Login == dto.Login);
            if (client == null)
            {
                throw new InvalidLoginException();
            }

            if (IsPasswordCorrect(dto.Password, client.Password))
                return CreateToken();

            else throw new InvalidPasswordException();
        }

        public CampaignDto CreateCampaign(CampaignCreationDto dto)
        {
            var buildings = GetTwoBuildings(dto.FromIdBuilding, dto.ToIdBuilding);
            var divideBuildings = _dividingDividingService.DivideBuildings(buildings);
            var leftBanner = _bannerService.CreateBanner(divideBuildings.First, dto.PricePerSquareMeter);
            var rightBanner = _bannerService.CreateBanner(divideBuildings.Second, dto.PricePerSquareMeter);
            var listOfBanners = new List<Banner> {leftBanner, rightBanner};
            var campaign = AddCampaign(dto,buildings,listOfBanners);
            _bannerService.UpdateBanners(campaign, leftBanner, rightBanner);
            _context.SaveChanges();
            return ConvertCampaignToCampaignDto(campaign);

        }

        private CampaignDto ConvertCampaignToCampaignDto(Campaign campaign)
        {

            return new CampaignDto
            {
                Client = GetClientInformation(campaign.Client),
                Banners = GetBannersDto(campaign.Banners),
                EndDate = campaign.EndDate,
                StartDate = campaign.StartDate,
                FromBuilding = campaign.FromBuilding,
                ToBuilding = campaign.ToBuilding,
                PricePerSquareMeter = campaign.PricePerSquareMeter
            };
        }

        private List<BannerDto> GetBannersDto(List<Banner> banners)
        {
            return banners.Select(GetBannerInformation).ToList();
        }
        private BannerDto GetBannerInformation(Banner banner)
        {
            return new BannerDto
            {
                Area = banner.Area,
                Name = banner.Name,
                Price = banner.Price
            };
        }


        private Campaign AddCampaign(CampaignCreationDto dto,Pair<Building,Building> buildings, List<Banner> banners)
        {
            var client = GetClient(dto.IdClient);
                var campaign = _context.Campaigns.Add(new Campaign
            {
                Client = client,
                IdClient = dto.IdClient,
                EndDate = dto.EndDate,
                Banners = banners,
                FromBuilding = buildings.First,
                ToBuilding = buildings.Second,
                FromIdBuilding = buildings.First.IdBuilding,
                ToIdBuilding = buildings.Second.IdBuilding,
                StartDate = dto.StartDate,
                PricePerSquareMeter = dto.PricePerSquareMeter
            });
                _context.SaveChanges();
                return campaign.Entity;

        }


        private Client GetClient(int idClient)
        {
            var client = _context.Clients.First(c => c.IdClient == idClient);
            if (client == null)
                throw new NoSuchClientException();
            return client;
        }
        
        

       
            

        private JwtSecurityToken CreateToken()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Role, "client"),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["SecretKey"]));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

           return new JwtSecurityToken
            (
                issuer: "Issuer",
                audience: "Clients",
                claims: claims,
                expires: DateTime.Now.AddMinutes(10),
                signingCredentials: credentials
            );
           

        }

        private Pair<Building, Building> GetTwoBuildings(int fromId,int toId)
        {
       
            
            _context.SaveChanges();
            var first = _context.Buildings.FirstOrDefault(b => b.IdBuilding == fromId);
            var second = _context.Buildings.FirstOrDefault(b => b.IdBuilding == toId);
            if (first == null || second == null)
                throw new NoSuchBuildingException();
            if (first.Street != second.Street)
                throw new BuildingsOnDifferentStreetsException();
            return new Pair<Building, Building>(first,second);
        }
    


        private static bool IsRegistrationDtoValid(UserRegistrationDto dto)
        {
            return dto?.FirstName != null &&
                   dto.LastName != null &&
                   dto.Email != null &&
                   dto.Login != null &&
                   IsPhoneNumberValid(dto.Phone) &&
                   IsPasswordValid(dto.Password);
        }

        private  bool IsLoginTaken(string login)
        {
            return _context.Clients.Any(c => c.Login == login);
        }

        private static bool IsPhoneNumberValid(string phone)
        {
            return phone != null && (
                Regex1.IsMatch(phone) ||
                Regex2.IsMatch(phone));
        }

        private static bool IsPasswordValid(string password)
        {
            return password != null && password.Length >= 8;
        }
    }
}