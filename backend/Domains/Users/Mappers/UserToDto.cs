using HootOut.Contracts.Users.Dtos;
using HootOut.Users.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.Users.Mappers
{
    public static class UserToDto
    {
        public static UserDto ToDto(this UserInfo user)
        {
            return new UserDto
            {
                Uid = user.Uid,
                Username = user.Username,
                Email = user.Email,
                Password = user.Password
            };
        }
    }
}
