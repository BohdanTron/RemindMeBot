﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Testing.XUnit;
using Microsoft.Extensions.Localization;
using NSubstitute;
using NSubstitute.Core;
using RemindMeBot.Resources;
using Xunit.Abstractions;

namespace RemindMeBot.Tests.Unit.Common
{
    public abstract class BotTestBase
    {
        protected List<IMiddleware> Middlewares;

        protected IStringLocalizer<BotMessages> Localizer = Substitute.For<IStringLocalizer<BotMessages>>();

        protected BotTestBase(ITestOutputHelper output)
        {
            Middlewares = new List<IMiddleware> { new XUnitDialogTestLogger(output) };
            SetupLocalizer();
        }

        protected void ConfigureLocalization(string culture, string? localTimeZone = null, DateTimeOffset? localTimeStamp = null)
        {
            // Set culture for the current test
            SetCurrentCulture(culture);

            // Add TestLocalizationMiddleware so the culture will be the same in dialogs and tests
            Middlewares.Add(new TestLocalizationMiddleware(new CultureInfo(culture), localTimeZone, localTimeStamp));
        }

        protected static void SetCurrentCulture(string culture)
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CultureInfo.CurrentUICulture = new CultureInfo(culture);

            CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator = "/";
            CultureInfo.CurrentUICulture.DateTimeFormat.DateSeparator = "/";

            CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern =
                CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern.Replace(".", "/");

            CultureInfo.CurrentUICulture.DateTimeFormat.ShortDatePattern =
                CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern.Replace(".", "/");
        }

        private void SetupLocalizer()
        {
            var resourceManager = new ResourceManager(
                $"{typeof(BotMessages).Assembly.GetName().Name}.Resources.BotMessages", typeof(BotMessages).Assembly);

            Localizer[Arg.Any<string>()].Returns(LocalizedString);
            Localizer[Arg.Any<string>(), Arg.Any<object[]>()].Returns(LocalizedString);

            LocalizedString? LocalizedString(CallInfo info)
            {
                var key = (string) info[0];
                var value = resourceManager.GetString(key, CultureInfo.CurrentCulture);

                if (value is null) return null;

                if (info.Args().Length > 1 && info[1] is object[] args)
                {
                    value = string.Format(value, args);
                }

                return new LocalizedString(key, value);
            }
        }

    }
}
