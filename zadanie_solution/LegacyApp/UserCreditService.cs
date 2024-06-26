﻿using System;
using System.Collections.Generic;
using System.Threading;

namespace LegacyApp
{
    //Single responsibility principle prohibits having both data retrieving and disposing in one class
    public class UserCreditService: IUserCreditService
    {
        /// <summary>
        /// Simulating database
        /// </summary>
        private readonly Dictionary<string, int> _database =
            new Dictionary<string, int>
            {
                {"Kowalski", 200},
                {"Malewski", 20000},
                {"Smith", 10000},
                {"Doe", 3000},
                {"Kwiatkowski", 1000}
            };
        
        private readonly IDisposable _disposable;

        public UserCreditService(IDisposable disposable)
        {
            _disposable = disposable;
        }

        public void Dispose()
        {
            _disposable.Dispose();
        }
        
        /// <summary>
        /// This method is simulating contact with remote service which is used to get info about someone's credit limit
        /// </summary>
        /// <returns>Client's credit limit</returns>
        public int GetCreditLimit(string lastName)
        {
            var randomWaitingTime = new Random().Next(3000);
            Thread.Sleep(randomWaitingTime);

            if (_database.TryGetValue(lastName, out var limit))
                return limit;

            throw new ArgumentException($"Client {lastName} does not exist");
        }
    }
}