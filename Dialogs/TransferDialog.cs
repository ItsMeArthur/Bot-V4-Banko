﻿using Banko.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Core.Extensions;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Prompts;
using Microsoft.Bot.Builder.Prompts.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Banko.Dialogs
{
    /// <summary>
    /// Defines the dialog for doing a transfer
    /// </summary>
    public class TransferDialog : DialogSet
    {
        /// <summary>
        /// Defines a singleton instance of the dialog.
        /// </summary>
        public static TransferDialog Instance { get; } = new Lazy<TransferDialog>(new TransferDialog()).Value;

        /// <summary>
        /// The names of the inputs and prompts in this dialog.
        /// </summary>
        /// <remarks>We'll store the information gathered using these same names.</remarks>
        public struct Keys
        {
            /// <summary>
            ///  Key to use for LUIS entities as input.
            /// </summary>
            public const string LuisArgs = "LuisEntities";

            public const string AccountLabel = "AccountLabel";
            //public const string Money = "money";
            //public const string Payee = "Payee";
            //public const string Date = "datetimeV2";
            public const string Confirm = "confirmation";
        }


        /// <summary>
        /// Creates a new dialog instance.
        /// </summary>
        private TransferDialog()
        {
            // Add the prompts we'll be using in our dialog.
            Add(Keys.AccountLabel, new Microsoft.Bot.Builder.Dialogs.TextPrompt());
            //Add(Keys.Money, new Microsoft.Bot.Builder.Dialogs.TextPrompt());
            //Add(Keys.Payee, new Microsoft.Bot.Builder.Dialogs.TextPrompt());
            //Add(Keys.Date, new Microsoft.Bot.Builder.Dialogs.DateTimePrompt(Culture.English, null));
            Add(Keys.Confirm, new Microsoft.Bot.Builder.Dialogs.ConfirmPrompt(Culture.English));

            // Define and add the waterfall steps for our dialog.
            Add(nameof(TransferDialog), new WaterfallStep[]
            {
                // Begin a transfer.
                async (dc, args, next) =>
                {
                    // Initialize state.
                    if(args!=null && args.ContainsKey(Keys.LuisArgs))
                    {
                        // Add any LUIS entities to the active dialog state.
                        // Remove any values that don't validate, and convert the remainder to a dictionary.
                        var entities = (BankoLuisModel._Entities)args[Keys.LuisArgs];
                        dc.ActiveDialog.State = ValidateLuisArgs(entities);
                    }
                    else
                    {
                        // Begin without any information collected.
                        dc.ActiveDialog.State = new Dictionary<string,object>();
                    }

                    if (dc.ActiveDialog.State.ContainsKey(Keys.AccountLabel))
                    {
                        // If we already have the account label, continue on to the next waterfall step.
                        await next();
                    }
                    else
                    {
                        // Otherwise, query for the information.
                        await dc.Prompt(Keys.AccountLabel,
                            "Which account?", new PromptOptions
                            {
                                RetryPromptString = "Which account do you want to transfer from (Joint, Current, Savings etc)",
                            });
                    }
                },
                async (dc, args, next) =>
                {
                    if (!dc.ActiveDialog.State.ContainsKey(Keys.AccountLabel))
                    {
                        // Update state from the prompt result.
                        var answer = (string)args["Value"];
                        dc.ActiveDialog.State[Keys.AccountLabel] = answer;
                    }

                    await next();
                },
                async (dc, args, next) =>
                {
                    // Confirm the transfer.
                    await dc.Prompt(Keys.Confirm,
                        $"Ok. I'll make this transfer, is this correct?" +
                        $"from {dc.ActiveDialog.State[Keys.AccountLabel]} ", 
                        new PromptOptions
                        {
                            RetryPromptString = "Should I make the transfer for you? Please enter `yes` or `no`.",
                        });
                },
                async (dc, args, next) =>
                {
                    // Make the transfer or cancel the operation.
                    var confirmed = (bool)args["Confirmation"];
                    if (confirmed)
                    {
                        // Send a confirmation message: the typing activity indicates to the user that the bot is working on something, the delay simulates a process that takes some time, and the message simulates a confirmation message generated by the process.
                        var typing = Activity.CreateTypingActivity();
                        var delay = new Activity { Type = "delay", Value = 3000 };
                        await dc.Context.SendActivities(
                            new IActivity[]
                            {
                                typing, delay,
                                MessageFactory.Text("Your transfer is scheduled. Reference number: #K89HG38SZ")
                            });
                    }
                    else
                    {
                        // Cancel the reservation.
                        await dc.Context.SendActivity("Okay. We have canceled the transfer.");
                    }
                }
            });
        }

        /// <summary>
        /// Check whether each entity is valid and return valid ones in a dictionary.
        /// </summary>
        /// <param name="entities">The LUIS entities from the input arguments.</param>
        /// <returns>A dictionary of the valid entities.</returns>
        private Dictionary<string, object> ValidateLuisArgs(BankoLuisModel._Entities entities)
        {
            var result = new Dictionary<string, object>();

            // Check Account Label
            if (entities?.AccountLabel?.Any() is true)
            {
                var accountLabel = entities.AccountLabel.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
                if (accountLabel != null)
                {
                    result[Keys.AccountLabel] = accountLabel;
                }
            }

            return result;
        }
    }
}