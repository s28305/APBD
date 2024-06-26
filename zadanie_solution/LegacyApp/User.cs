﻿using System;

namespace LegacyApp
{
    public class User
    {
        public object Client { get; internal set; }
        public DateTime DateOfBirth { get; internal set; }
        public string EmailAddress { get; internal set; }
        public string FirstName { get; internal set; }
        public string LastName { get; internal set; }

        public CreditData CreditData { get; internal set; }

        public User(CreditData creditData)
        {
            CreditData = creditData;
        }
    }
}