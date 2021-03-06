﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Text;
using NLog;
using System.Threading;

namespace TelegaEventsBotDotNet
{

    class BotInput
    {
        private const int messageBlockSize = 3;
        private ChatStateHandler currentState = new ChatStateHandler(-1);
        private static Logger logger = LogManager.GetCurrentClassLogger();
 
        private BotCallbacks _callbacks;
        private Telegram.Bot.TelegramBotClient _bot;
        private SettingsWrapper _settingsWrapper;

        private Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup GetPreviewKeyboard(Int32 messageCount, Int32 blockSize)
        {
            Int32 pagesCount = messageCount / blockSize;
            if (messageCount % blockSize != 0)
                pagesCount++;

            var buttonLayout = new Telegram.Bot.Types.InlineKeyboardButtons.InlineKeyboardButton[pagesCount + 1][];
            int i = 0;
            for (i = 0; i < pagesCount; ++i)
            {
                buttonLayout[i] = new[] { new Telegram.Bot.Types.InlineKeyboardButtons.InlineKeyboardCallbackButton(i.ToString(), "ListCallBack:" + i.ToString()) };
            }
            buttonLayout[i] = new[] { new Telegram.Bot.Types.InlineKeyboardButtons.InlineKeyboardCallbackButton("В начало!", "StartSearchMessage") };
            return new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(buttonLayout);

        }

        private Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup BuildKeyboardFromMessage(MessageWithButtons message)
        {
            var buttonLayout = new Telegram.Bot.Types.InlineKeyboardButtons.InlineKeyboardButton[message.Buttons.Count][];
            int i = 0;
            for (i = 0; i < message.Buttons.Count; ++i)
            {
                buttonLayout[i] = new[] { new Telegram.Bot.Types.InlineKeyboardButtons.InlineKeyboardCallbackButton(message.Buttons[i].Text, message.Buttons[i].Callback) };
            }
            return new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(buttonLayout);
        }

        public BotInput(Telegram.Bot.TelegramBotClient BotClient, ChatStateHandler state)
        {
            currentState = state;
            var path = System.IO.Path.GetDirectoryName( 
                System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
            path = path.Substring(6);
            _settingsWrapper = new SettingsWrapper(Directory.GetCurrentDirectory()+"\\Messages.xml");
            _bot = BotClient;
            _callbacks = new BotCallbacks(this, _bot);
        }

        public void StartSearchMessage(long ChatID, int ReplyMessageId = 0)
        {
            currentState.CurrentBotState = BotState.IDLE;
            var message = _settingsWrapper.StartSearchMessage();
            var keyboard = BuildKeyboardFromMessage(message);
            _bot.SendTextMessageAsync(ChatID, message.Text, Telegram.Bot.Types.Enums.ParseMode.Markdown, false, false, 0, keyboard);
            logger.Info("Client connected and asked to start.");
            Console.WriteLine("Client connected and asked to start.");
        }

        public void SearchNerbyDateEventsMessage(long ChatID, int ReplyMessageId = 0)
        {
            var message = _settingsWrapper.SearchNearbyDate();
            var keyboard = BuildKeyboardFromMessage(message);
            _bot.SendTextMessageAsync(ChatID, message.Text, Telegram.Bot.Types.Enums.ParseMode.Markdown, false, false, 0, keyboard);
            Console.WriteLine("Client asked for nearby events.");
        }

        public void SearchByKeywordsMessage(long ChatID, int ReplyMessageId = 0)
        {
            var message = _settingsWrapper.SearchByKeywords();
            var keyboard = BuildKeyboardFromMessage(message);
            _bot.SendTextMessageAsync(ChatID, message.Text, Telegram.Bot.Types.Enums.ParseMode.Markdown, false, false, 0, keyboard);
            currentState.CurrentBotState = BotState.AWAITINGKEYWORD;
            Console.WriteLine("Client asked for keywords search.");          
        }

        private RLEvent SearchRandomInBetween(DateTime start, DateTime finish)
        {
            DatabaseWrapper db = new DatabaseWrapper();
            var events = db.GetEventsBetween(start, finish);
            if (events.Count > 0)
                return events.RandomElement();
            else return new RLEvent();
        }

        public void SearchNerbyRandomToday(long chatId, int messageId = 0)
        {
            DateTime start = DateTime.Now;
            DateTime finish = DateTime.Today;
            finish = finish.AddDays(1);
            RLEvent rLEvent = SearchRandomInBetween(start, finish);
            _bot.SendTextMessageAsync(chatId, _settingsWrapper.ParseEvent(rLEvent), Telegram.Bot.Types.Enums.ParseMode.Markdown);
            Thread.Sleep(50);
            var message = _settingsWrapper.RepeatSearchRandom();
            var keyboard = BuildKeyboardFromMessage(message);
            _bot.SendTextMessageAsync(chatId, message.Text, Telegram.Bot.Types.Enums.ParseMode.Markdown, false, false, 0, keyboard);
            logger.Info("Sent event to client, id: {0}.", rLEvent.EventId);
            Console.WriteLine("Sent event to client, id: {0}.", rLEvent.EventId);
        }

        public void SearchNerbyRandomTomorrow(long chatId, int messageId = 0)
        {
            DateTime start = DateTime.Today;
            DateTime finish = DateTime.Today;
            finish = finish.AddDays(2);
            start = start.AddDays(1);
            RLEvent rLEvent = SearchRandomInBetween(start, finish);
            _bot.SendTextMessageAsync(chatId, _settingsWrapper.ParseEvent(rLEvent), Telegram.Bot.Types.Enums.ParseMode.Markdown);
            Thread.Sleep(50);
            var message = _settingsWrapper.RepeatSearchRandom();
            var keyboard = BuildKeyboardFromMessage(message);
            _bot.SendTextMessageAsync(chatId, message.Text, Telegram.Bot.Types.Enums.ParseMode.Markdown, false, false, 0, keyboard);
            Console.WriteLine("Sent event to client, id: {0}.", rLEvent.EventId);
        }

        public void SearchNerbyRandomThisWeek(long chatId, int messageId = 0)
        {
            DateTime start = DateTime.Now;
            DateTime finish = DateTime.Today;
            finish = finish.AddDays(7);
            RLEvent rLEvent = SearchRandomInBetween(start, finish);
            _bot.SendTextMessageAsync(chatId, _settingsWrapper.ParseEvent(rLEvent), Telegram.Bot.Types.Enums.ParseMode.Markdown);
            Thread.Sleep(50);
            var message = _settingsWrapper.RepeatSearchRandom();
            var keyboard = BuildKeyboardFromMessage(message);
            _bot.SendTextMessageAsync(chatId, message.Text, Telegram.Bot.Types.Enums.ParseMode.Markdown, false, false, 0, keyboard);
            Console.WriteLine("Sent event to client, id: {0}.", rLEvent.EventId);
        }


        public void HandleCommand(String Command, long ChatID, int ReplyMessageId = 0)
        {
            switch (Command)
            {
                case "/say":
                    break;
                case "/start":
                    StartSearchMessage(ChatID);
                    break;
                default:
                    if (currentState.CurrentBotState == BotState.AWAITINGKEYWORD)
                        SearchKeywords(Command, ChatID, ReplyMessageId);
                    if (currentState.CurrentBotState == BotState.AWAITINGDATE)
                        SearchByDateHandler(Command, ChatID, ReplyMessageId);
                    if (currentState.CurrentBotState == BotState.AWAITINGPAGE)
                        SendFullAfterPreview(Command, ChatID, ReplyMessageId);
                    return;
            }
        }

        public void SearchByDate(long ChatID, int ReplyMessageId = 0)
        {
            var message = _settingsWrapper.SearchByDate();
            var keyboard = BuildKeyboardFromMessage(message);
            _bot.SendTextMessageAsync(ChatID, message.Text, Telegram.Bot.Types.Enums.ParseMode.Markdown, false, false, 0, keyboard);
            currentState.CurrentBotState = BotState.AWAITINGDATE;
            Console.WriteLine("Client asked for date search.");
        }

        public void SendFullAfterPreview(String Command, long ChatID, int ReplyMessageId = 0)
        {
            Int64 parsedId = -1;
            String toParse = Command.Remove(0, 1);
            if (Int64.TryParse(toParse, out parsedId))
            {
                DatabaseWrapper db = new DatabaseWrapper();
                var _event = db.GetEventById(parsedId);
                var test = _bot.SendTextMessageAsync(ChatID, _settingsWrapper.ParseEvent(_event), Telegram.Bot.Types.Enums.ParseMode.Markdown);
                Thread.Sleep(100);
                var message = _settingsWrapper.StartSearchMessage(); //ВОТ ТУТ ЧИНИТЬ
                var keyboard = BuildKeyboardFromMessage(message);
                _bot.SendTextMessageAsync(ChatID, message.Text, Telegram.Bot.Types.Enums.ParseMode.Markdown, false, false, 0, keyboard);
            }

        }

        public void EditMessagePreview(Int32 blockNumber, long ChatID, int ReplyMessageId = 0)
        {
            _bot.EditMessageTextAsync(ChatID, currentState.CurrentPreviewMessageId, currentState.messagBlocks[blockNumber], 
                Telegram.Bot.Types.Enums.ParseMode.Markdown, false, GetPreviewKeyboard(currentState.MessagesInResult, messageBlockSize));
        }

        public bool HandleEventsPrinting(List<RLEvent> events, long ChatID, int ReplyMessageId = 0)
        {
            List<String> messageBlocks = new List<string>();
            messageBlocks.Add("");
            if (events.Count <= 3)
            {
                foreach (var _event in events)
                {
                    var test = _bot.SendTextMessageAsync(ChatID, _settingsWrapper.ParseEvent(_event), Telegram.Bot.Types.Enums.ParseMode.Markdown);
                }
                return true;
            } else
            {
                currentState.MessagesInResult = events.Count;
                int counter = 0;
                int blockCounter = 0;
                foreach (var _event in events)
                {
                    if (counter == 3)
                    {
                        counter = 0;
                        messageBlocks.Add("");
                        blockCounter++;
                    }
                    messageBlocks[blockCounter] += (_settingsWrapper.ParsePreviewEvent(_event) + "Подробнее: /" + _event.EventId + '\n');    
                    counter++;
                }

                currentState.messagBlocks = messageBlocks;
                var test = _bot.SendTextMessageAsync(ChatID, messageBlocks[0], Telegram.Bot.Types.Enums.ParseMode.Markdown, false, false, 0, GetPreviewKeyboard(events.Count, messageBlockSize)).Result;
                currentState.CurrentBotState = BotState.AWAITINGPAGE;
                currentState.PreviewPage = 0;
                currentState.CurrentPreviewMessageId = test.MessageId;
                return false;
            }

        }

        public void SearchByDateHandler(String Command, long ChatID, int ReplyMessageId = 0)
        {
            DateTime startDate = DateTime.Today;
            if (DateTime.TryParse(Command, out startDate))
            {
                DateTime finishDate = startDate.AddDays(1);
                DatabaseWrapper db = new DatabaseWrapper();
                var events = db.GetEventsBetween(startDate, finishDate);
                var message = _settingsWrapper.RepeatSearchByDateNotFound();
                var keyboard = BuildKeyboardFromMessage(message);
                if (events.Count == 0)
                {
                    _bot.SendTextMessageAsync(ChatID, message.Text, Telegram.Bot.Types.Enums.ParseMode.Markdown, false, false, 0, keyboard);
                    return;
                }
                if (HandleEventsPrinting(events, ChatID, ReplyMessageId))
                {
                    Thread.Sleep(100);
                    message = _settingsWrapper.RepeatSearchByDateSuccess();
                    keyboard = BuildKeyboardFromMessage(message);
                    _bot.SendTextMessageAsync(ChatID, message.Text, Telegram.Bot.Types.Enums.ParseMode.Markdown, false, false, 0, keyboard);
                }


            } else
            {
                var message = _settingsWrapper.RepeatSearchByDate();
                var keyboard = BuildKeyboardFromMessage(message);
                _bot.SendTextMessageAsync(ChatID, message.Text, Telegram.Bot.Types.Enums.ParseMode.Markdown, false, false, 0, keyboard);
            }
        }

        public void SearchKeywords(String Command, long ChatID, int ReplyMessageId = 0)
        {
            currentState.CurrentBotState = BotState.IDLE;
            string[] keyWords = Command.Split(' ');
            DatabaseWrapper db = new DatabaseWrapper();
            var events = db.GetEventIDsByKeyword(new List<string>(keyWords));
            var message = _settingsWrapper.NoEventsFound();
            var keyboard = BuildKeyboardFromMessage(message);
            if (events.Count == 0)
            {
                _bot.SendTextMessageAsync(ChatID, message.Text, Telegram.Bot.Types.Enums.ParseMode.Markdown, false, false, 0, keyboard);
            }
            if (HandleEventsPrinting(events, ChatID, ReplyMessageId))
            {
                Thread.Sleep(100);
                message = _settingsWrapper.RepeatSearchByKeywords();
                keyboard = BuildKeyboardFromMessage(message);
                _bot.SendTextMessageAsync(ChatID, message.Text, Telegram.Bot.Types.Enums.ParseMode.Markdown, false, false, 0, keyboard);
            }
        }

        public void HandleCallback(String Callback, long ChatID, int ReplyMessageId = 0)
        {
            _callbacks.HandleCallback(Callback, ChatID, ReplyMessageId);
        }
    }
}
