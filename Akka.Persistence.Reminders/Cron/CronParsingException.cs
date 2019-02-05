#region copyright
// -----------------------------------------------------------------------
//  <copyright file="CronParsingException.cs" creator="Bartosz Sypytkowski">
//      Copyright (C) 2019 Bartosz Sypytkowski <b.sypytkowski@gmail.com>
//  </copyright>
// -----------------------------------------------------------------------
#endregion

using System;
using Akka.Actor;

namespace Akka.Persistence.Reminders.Cron
{
    public class CronParsingException : AkkaException
    {
        public CronParsingException(string message) : base(message)
        {
        }
    }
}