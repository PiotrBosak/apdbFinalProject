﻿using System.IdentityModel.Tokens.Jwt;
using APDB_Project.Dtos;

namespace APDB_Project.Services
{
    public interface IUserService
    {
        public JwtSecurityToken RegisterUser(UserRegistrationDto dto);
        public JwtSecurityToken LoginUser(UserLoginDto dto);
    }
}