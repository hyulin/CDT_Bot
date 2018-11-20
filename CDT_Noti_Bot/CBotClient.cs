﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Timers;
using System.Threading;
using System.Xml.XPath;

//Telegram API
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

// Google Sheet API
using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

// HtmlAgilityPack
using HtmlAgilityPack;

namespace CDT_Noti_Bot
{
    class CBotClient
    {
        CSystemInfo systemInfo = new CSystemInfo();     // 시스템 정보

        // If modifying these scopes, delete your previously saved credentials
        // at ~/.credentials/sheets.googleapis.com-dotnet-quickstart.json
        static string[] Scopes = { SheetsService.Scope.Spreadsheets };
        static string ApplicationName = "Clien Delicious Team Notice Bot";
        UserCredential credential;
        SheetsService service;
        CNotice Notice = new CNotice();
        CEasterEgg EasterEgg = new CEasterEgg();
        CUserDirector userDirector = new CUserDirector();

        // Bot Token
#if DEBUG
        const string strBotToken = "624245556:AAHJQ3bwdUB6IRf1KhQ2eAg4UDWB5RTiXzI";     // 테스트 봇 토큰
#else
        const string strBotToken = "648012085:AAHxJwmDWlznWTFMNQ92hJyVwsB_ggJ9ED8";     // 봇 토큰
#endif

        private Telegram.Bot.TelegramBotClient Bot = new Telegram.Bot.TelegramBotClient(strBotToken);

        public void InitBotClient()
        {
            systemInfo.SetStartTime();

            using (var stream =
                new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            // Create Google Sheets API service.
            service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });


            // 시트에서 유저 정보를 Load
            loadUserInfo();


            // 타이머 생성 및 시작
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Interval = 5000; // 5초
            timer.Elapsed += new ElapsedEventHandler(timer_Elapsed);
            timer.Start();
        }

        // init methods...
        public async void telegramAPIAsync()
        {
            //Bot 에 대한 정보를 가져온다.
            var me = await Bot.GetMeAsync();
        }

        public void setTelegramEvent()
        {
            Bot.OnMessage += Bot_OnMessage;     // 이벤트를 추가해줍니다. 

            Bot.StartReceiving();               // 이 함수가 실행이 되어야 사용자로부터 메세지를 받을 수 있습니다.
        }

        public void loadUserInfo()
        {
            // Define request parameters.
            String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
            String range = "클랜원 목록!C7:N";
            SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);

            ValueRange response = request.Execute();
            if (response != null)
            {
                IList<IList<Object>> values = response.Values;
                if (values != null && values.Count > 0)
                {
                    foreach (var row in values)
                    {
                        bool isReflesh = false;

                        // 아테나에 등록되지 않은 유저
                        if (row[10].ToString() == "")
                            continue;

                        long userKey = Convert.ToInt64(row[10].ToString());
                        var userData = userDirector.getUserInfo(userKey);
                        if (userData.UserKey != 0)
                        {
                            // 이미 등록한 유저. 갱신한다.
                            isReflesh = true;
                        }

                        CUser user = new CUser();
                        user = setUserInfo(row, Convert.ToInt64(row[10].ToString()));

                        // 휴린, 냉각콜라, 만슬, 청포도일 경우
                        if ( (user.UserKey == 23842788) || (user.UserKey == 50872681) || (user.UserKey == 474057213) || (user.UserKey == 35432635) )
                        {
                            // 유저 타입을 관리자로
                            user.UserType = USER_TYPE.USER_TYPE_ADMIN;
                        }

                        if (isReflesh == false)
                            userDirector.AddUserInfo(userKey, user);
                        else
                            userDirector.reflechUserInfo(userKey, user);
                    }
                }
            }
        }

        public CUser setUserInfo(IList<object> row, long userKey)
        {
            CUser user = new CUser();

            if (row.Count == 0)
                return user;
            
            user.UserKey = userKey;
            user.Name = row[0].ToString();
            user.MainBattleTag = row[1].ToString();
            user.SubBattleTag = row[2].ToString().Trim().Split(',');

            if (row[3].ToString() == "플렉스")
                user.Position |= POSITION.POSITION_FLEX;
            if (row[3].ToString().ToUpper().Contains("딜"))
                user.Position |= POSITION.POSITION_DPS;
            if (row[3].ToString().ToUpper().Contains("탱"))
                user.Position |= POSITION.POSITION_TANK;
            if (row[3].ToString().ToUpper().Contains("힐"))
                user.Position |= POSITION.POSITION_SUPP;

            string[] most = new string[3];
            most[0] = row[4].ToString();
            most[1] = row[5].ToString();
            most[2] = row[6].ToString();
            user.MostPick = most;

            user.OtherPick = row[7].ToString();
            user.Time = row[8].ToString();
            user.Info = row[9].ToString();

            // 휴린, 냉각콜라, 만슬, 청포도일 경우
            if ((user.UserKey == 23842788) || (user.UserKey == 50872681) || (user.UserKey == 474057213) || (user.UserKey == 35432635))
            {
                // 유저 타입을 관리자로
                user.UserType = USER_TYPE.USER_TYPE_ADMIN;
            }

            return user;
        }

        public Tuple<int, string> referenceScore(string battleTag)
        {
            int score = 0;
            string tier = "";

            string[] strBattleTag = battleTag.Split('#');
            string strUrl = "http://playoverwatch.com/ko-kr/career/pc/" + strBattleTag[0] + "-" + strBattleTag[1];

            try
            {
                WebClient wc = new WebClient();
                wc.Encoding = Encoding.UTF8;

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

                string html = wc.DownloadString(strUrl);
                HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                string strScore = doc.DocumentNode.SelectSingleNode("//div[@class='competitive-rank']").InnerText;
                score = Convert.ToInt32(strScore);

                if (score == 0)
                {
                    tier = "Unranked";
                }
                else if (score >= 0 && score < 1500)
                {
                    tier = "브론즈";
                }
                else if (score >= 1500 && score < 2000)
                {
                    tier = "실버";
                }
                else if (score >= 2000 && score < 2500)
                {
                    tier = "골드";
                }
                else if (score >= 2500 && score < 3000)
                {
                    tier = "플래티넘";
                }
                else if (score >= 3000 && score < 3500)
                {
                    tier = "다이아";
                }
                else if (score >= 3500 && score < 4000)
                {
                    tier = "마스터";
                }
                else if (score >= 4000 && score <= 5000)
                {
                    tier = "그랜드마스터";
                }
            }
            catch
            {
                // 아무 작업 안함
            }

            Tuple<int, string> retTuple = Tuple.Create(score, tier);
            return retTuple;
        }

        // 쓰레드풀의 작업쓰레드가 지정된 시간 간격으로
        // 아래 이벤트 핸들러 실행
        public void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            string strPrint = "";

            // Define request parameters.
            String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
            String range = "클랜 공지!C15:C23";
            String updateRange = "클랜 공지!H14";
            SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);
            SpreadsheetsResource.ValuesResource.GetRequest updateRequest = service.Spreadsheets.Values.Get(spreadsheetId, updateRange);

            ValueRange response = request.Execute();
            ValueRange updateResponse = updateRequest.Execute();

            if (response != null && updateResponse != null)
            {
                IList<IList<Object>> values = response.Values;
                IList<IList<Object>> updateValues = updateResponse.Values;

                if (updateValues != null && updateValues.ToString() != "")
                {
                    if (values != null && values.Count > 0)
                    {
                        strPrint += "#공지사항\n\n";

                        foreach (var row in values)
                        {
                            strPrint += "* " + row[0] + "\n\n";
                        }
                    }

                    Notice.SetNotice(strPrint);

                    // Define request parameters.
                    ValueRange valueRange = new ValueRange();
                    valueRange.MajorDimension = "COLUMNS"; //"ROWS";//COLUMNS 

                    var oblist = new List<object>() { "" };
                    valueRange.Values = new List<IList<object>> { oblist };

                    SpreadsheetsResource.ValuesResource.UpdateRequest releaseRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, updateRange);

                    releaseRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                    UpdateValuesResponse releaseResponse = releaseRequest.Execute();
                    if (releaseResponse == null)
                    {
                        strPrint = "[ERROR] 시트를 업데이트 할 수 없습니다.";
                    }

#if DEBUG
                    Bot.SendTextMessageAsync(-1001312491933, strPrint);  // 운영진방
#else
                    Bot.SendTextMessageAsync(-1001202203239, strPrint);  // 클랜방
#endif
                }
            }
        }

        //Events...
        // Telegram...
        private async void Bot_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            var varMessage = e.Message;

            if (varMessage == null || (varMessage.Type != MessageType.Text && varMessage.Type != MessageType.ChatMembersAdded))
            {
                return;
            }

            DateTime convertTime = varMessage.Date.AddHours(9);
            if (convertTime < systemInfo.GetStartTimeToDate())
            {
                return;
            }

            string strFirstName = varMessage.From.FirstName;
            string strLastName = varMessage.From.LastName;
            int iMessageID = varMessage.MessageId;
            long senderKey = varMessage.From.Id;

            // CDT 관련방 아니면 동작하지 않도록 수정
            if (varMessage.Chat.Id != -1001202203239 &&     // 본방
                varMessage.Chat.Id != -1001312491933 &&     // 운영진방
                varMessage.Chat.Id != -1001389956706 &&     // 사전안내방
                varMessage.Chat.Username != "hyulin")
            {
                await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 사용할 수 없는 대화방입니다.", ParseMode.Default, false, false, iMessageID);
                return;
            }

            // 명령어, 서브명령어 분리
            string strMassage = varMessage.Text;
            string strUserName = varMessage.From.FirstName + varMessage.From.LastName;
            string strCommend = "";
            string strContents = "";
            bool isCommand = false;

            // 명령어인지 아닌지 구분
            if (strMassage.Substring(0, 1) == "/")
            {
                isCommand = true;

                // 명령어와 서브명령어 구분
                if (strMassage.IndexOf(" ") == -1)
                {
                    strCommend = strMassage;
                }
                else
                {
                    strCommend = strMassage.Substring(0, strMassage.IndexOf(" "));
                    strContents = strMassage.Substring(strMassage.IndexOf(" ") + 1, strMassage.Count() - strMassage.IndexOf(" ") - 1);
                }

                // 미등록 유저는 사용할 수 없다.
                if (strCommend != "/등록" && userDirector.getUserInfo(senderKey).UserKey == 0)
                {
                    await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 아테나에 등록되지 않은 유저입니다.\n등록을 하시려면 /등록 명령어를 참고해주세요.", ParseMode.Default, false, false, iMessageID);
                    return;
                }
            }

            // 이스터에그 (아테나 대사 출력)
            if (userDirector.getUserInfo(senderKey).UserKey != 0 && varMessage.ReplyToMessage != null && varMessage.ReplyToMessage.From.FirstName.Contains("아테나") == true)
            {
                // 등록된 유저가 시도했을 경우 출력
                await Bot.SendTextMessageAsync(varMessage.Chat.Id, EasterEgg.GetEasterEgg(), ParseMode.Default, false, false, iMessageID);
                return;
            }

            // 입장 메시지 일 경우
            if (varMessage.Type == MessageType.ChatMembersAdded)
            {
                if (varMessage.Chat.Id == -1001389956706)   // 사전안내방
                {
                    varMessage.Text = "/안내";
                }
                else if (varMessage.Chat.Id == -1001202203239)      // 본방
                {
                    string strInfo = "";

                    strInfo += "\n안녕하세요.\n";
                    strInfo += "서로의 삶에 힘이 되는 오버워치 클랜,\n";
                    strInfo += "'클리앙 딜리셔스 팀'에 오신 것을 환영합니다.\n\n";
                    strInfo += "저는 팀의 운영 봇인 아테나입니다.\n";
                    strInfo += "\n";
                    strInfo += "클랜 생활에 불편하신 점이 있으시거나\n";
                    strInfo += "건의사항, 문의사항이 있으실 때는\n";
                    strInfo += "운영자 냉각콜라(@Seungman),\n";
                    strInfo += "운영자 만슬(@mans3ul)에게 문의해주세요.\n";
                    strInfo += "\n";
                    strInfo += "우리 클랜의 모든 일정관리 및 운영은\n";
                    strInfo += "통합문서를 통해 확인 하실 수 있습니다.\n";
                    strInfo += "(https://goo.gl/nurbLT [딜리셔스.kr])\n";
                    strInfo += "통합 문서에 대해 문의사항이 있으실 때는\n";
                    strInfo += "운영자 청포도(@leetk321)에게 문의해주세요.\n";
                    strInfo += "\n";
                    strInfo += "클랜원들의 편의를 위한\n";
                    strInfo += "저, 아테나의 기능을 확인하시려면\n";
                    strInfo += "/도움말 을 입력해주세요.\n";
                    strInfo += "편리한 기능들이 많이 있으며,\n";
                    strInfo += "앞으로 더 추가될 예정입니다.\n";
                    strInfo += "아테나에 대해 문의사항이 있으실 때는\n";
                    strInfo += "운영자 휴린(@hyulin)에게 문의해주세요.\n";
                    strInfo += "\n";
                    strInfo += "저희 CDT에서 즐거운 오버워치 생활,\n";
                    strInfo += "그리고 더 나아가 즐거운 라이프를\n";
                    strInfo += "즐기셨으면 좋겠습니다.\n\n";
                    strInfo += "잘 부탁드립니다 :)\n";

                    const string record = @"Function/Logo.jpg";
                    var fileName = record.Split(Path.DirectorySeparatorChar).Last();
                    var fileStream = new FileStream(record, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await Bot.SendPhotoAsync(varMessage.Chat.Id, fileStream, strInfo);

                    return;
                }
                else
                {
                    return;
                }
            }

            // 명령어가 아닐 경우 아래는 태울 필요 없다.
            if (isCommand == false)
            {
                return;
            }

            string strPrint = "";

            //========================================================================================
            // 도움말
            //========================================================================================
            if (strCommend == "/도움말" || strCommend == "/help" || strCommend == "/help@CDT_Noti_Bot")
            {
                strPrint += "==================================\n";
                strPrint += "[ 아테나 v1.5 ]\n[ Clien Delicious Team Notice Bot ]\n\n";
                strPrint += "/공지 : 팀 공지사항을 출력합니다.\n";
                strPrint += "/등록 [본 계정 배틀태그] : 아테나에 등록 합니다.\n";
                strPrint += "/조회 [검색어] : 클랜원을 조회합니다.\n";
                strPrint += "               (검색범위 : 대화명, 배틀태그, 부계정)\n";
                strPrint += "/영상 : 영상이 있던 날짜를 조회합니다.\n";
                strPrint += "/영상 [날짜] : 플레이 영상을 조회합니다. (/영상 181006)\n";
                strPrint += "/검색 [검색어] : 포지션, 모스트별로 클랜원을 검색합니다.\n";
                strPrint += "/스크림 : 현재 모집 중인 스크림의 참가자를 출력합니다.\n";
                strPrint += "/스크림 [요일] : 현재 모집 중인 스크림에 참가신청합니다.\n";
                strPrint += "/스크림 취소 : 신청한 스크림에 참가를 취소합니다.\n";
                strPrint += "/조사 : 현재 진행 중인 일정 조사를 출력합니다.\n";
                strPrint += "/조사 [요일] : 현재 진행 중인 일정 조사에 체크합니다.\n";
                strPrint += "/모임 : 모임 공지와 참가자를 출력합니다.\n";
                strPrint += "/참가 : 모임에 참가 신청합니다.\n";
                strPrint += "/참가 확정 : 모임에 참가 확정합니다.\n";
                strPrint += "       (이미 참가일 경우 확정만 체크)\n";
                strPrint += "/불참 : 모임에 참가 신청을 취소합니다.\n";
                strPrint += "/투표 : 현재 진행 중인 투표를 출력합니다.\n";
                strPrint += "/투표 [숫자] : 현재 진행 중인 투표에 투표합니다.\n";
                strPrint += "/투표 결과 : 현재 진행 중인 투표의 결과를 출력합니다.\n";
                strPrint += "/기록 : 클랜 명예의 전당을 조회합니다.\n";
                strPrint += "/기록 [숫자] : 명예의 전당 상세내용을 조회합니다.\n";
                strPrint += "/안내 : 팀 안내 메시지를 출력합니다.\n";
                strPrint += "/상태 : 현재 봇 상태를 출력합니다. 대답이 없으면 이상.\n";
                strPrint += "----------------------------------\n";
                strPrint += "CDT 1대 운영자 : 냉각콜라, 휴린, 청포도, 만슬\n";
                strPrint += "==================================\n";
                strPrint += "버그 및 문의사항이 있으시면 '휴린'에게 문의해주세요. :)\n";
                strPrint += "==================================\n";

                await Bot.SendTextMessageAsync(varMessage.Chat.Id, strPrint, ParseMode.Default, false, false, iMessageID);
            }
            //========================================================================================
            // 등록
            //========================================================================================
            else if (strCommend == "/등록")
            {
                if (strContents == "")
                {
                    strPrint += "[SYSTEM] 사용자 등록을 하려면\n/등록 [본 계정 배틀태그] 로 등록해주세요.\n(ex: /등록 휴린#3602)";
                }
                else
                {
                    string battleTag = strContents;

                    // Define request parameters.
                    String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                    String range = "클랜원 목록!C7:N";
                    SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);

                    ValueRange response = request.Execute();
                    if (response != null)
                    {
                        IList<IList<Object>> values = response.Values;
                        if (values != null && values.Count > 0)
                        {
                            int index = 0;
                            int searchIndex = 0;
                            int searchCount = 0;
                            bool isReflesh = false;
                            CUser user = new CUser();

                            foreach (var row in values)
                            {
                                // 검색 성공
                                if (battleTag == row[1].ToString())
                                {
                                    long userKey = 0;
                                    searchCount++;
                                    searchIndex = index;

                                    if (row[10].ToString() != "")
                                    {
                                        // 이미 값이 있으므로 갱신한다.
                                        userKey = Convert.ToInt64(row[10].ToString());
                                        isReflesh = true;
                                    }
                                    else
                                    {
                                        userKey = senderKey;
                                    }

                                    user.UserKey = userKey;
                                    user.Name = row[0].ToString();
                                    user.MainBattleTag = row[1].ToString();
                                    user.SubBattleTag = row[2].ToString().Trim().Split(',');

                                    if (row[3].ToString() == "플렉스")
                                        user.Position |= POSITION.POSITION_FLEX;
                                    if (row[3].ToString().ToUpper().Contains("딜"))
                                        user.Position |= POSITION.POSITION_DPS;
                                    if (row[3].ToString().ToUpper().Contains("탱"))
                                        user.Position |= POSITION.POSITION_TANK;
                                    if (row[3].ToString().ToUpper().Contains("힐"))
                                        user.Position |= POSITION.POSITION_SUPP;

                                    string[] most = new string[3];
                                    most[0] = row[4].ToString();
                                    most[1] = row[5].ToString();
                                    most[2] = row[6].ToString();
                                    user.MostPick = most;

                                    user.OtherPick = row[7].ToString();
                                    user.Time = row[8].ToString();
                                    user.Info = row[9].ToString();
                                }
                                else
                                {
                                    index++;
                                }
                            }

                            if (searchCount == 0)
                            {
                                strPrint += "[ERROR] 배틀태그를 검색할 수 없습니다.";
                            }
                            else if (searchCount > 1)
                            {
                                strPrint += "[ERROR] 검색 결과가 2개 이상입니다. 배틀태그를 확인해주세요.";
                            }
                            else if (searchCount < 0)
                            {
                                strPrint += "[ERROR] 알 수 없는 문제";
                            }
                            else if (isReflesh == true)
                            {
                                if (userDirector.reflechUserInfo(user.UserKey, user) == true)
                                {
                                    strPrint += "[SUCCESS] 갱신 완료됐습니다.";
                                }
                                else
                                {
                                    strPrint += "[ERROR] 갱신을 실패했습니다.";
                                }
                            }
                            else
                            {
                                range = "클랜원 목록!M" + (7 + searchIndex);

                                // Define request parameters.
                                ValueRange valueRange = new ValueRange();
                                valueRange.MajorDimension = "COLUMNS"; //"ROWS";//COLUMNS 

                                var oblist = new List<object>() { senderKey };
                                valueRange.Values = new List<IList<object>> { oblist };

                                SpreadsheetsResource.ValuesResource.UpdateRequest updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, range);

                                updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                                UpdateValuesResponse updateResponse = updateRequest.Execute();

                                if (updateResponse == null)
                                {
                                    strPrint += "[ERROR] 시트를 업데이트 할 수 없습니다.";
                                }
                                else
                                {
                                    strPrint += "[SUCCESS] 등록이 완료됐습니다.";
                                    userDirector.AddUserInfo(senderKey, user);
                                }
                            }
                        }
                    }
                }

                await Bot.SendTextMessageAsync(varMessage.Chat.Id, strPrint, ParseMode.Default, false, false, iMessageID);
            }
            //========================================================================================
            // 공지사항
            //========================================================================================
            else if (strCommend == "/공지")
            {
                // Define request parameters.
                String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                String range = "클랜 공지!C15:C23";
                SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);

                ValueRange response = request.Execute();
                if (response != null)
                {
                    IList<IList<Object>> values = response.Values;
                    if (values != null && values.Count > 0)
                    {
                        strPrint += "#공지사항\n\n";

                        foreach (var row in values)
                        {
                            strPrint += "* " + row[0] + "\n\n";
                        }
                    }
                }

                if (strPrint != "")
                {
                    const string notice = @"Function/Logo.jpg";
                    var fileName = notice.Split(Path.DirectorySeparatorChar).Last();
                    var fileStream = new FileStream(notice, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await Bot.SendPhotoAsync(varMessage.Chat.Id, fileStream, strPrint, ParseMode.Default, false, iMessageID);
                }
                else
                {
                    await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 공지가 등록되지 않았습니다.", ParseMode.Default, false, false, iMessageID);
                }
            }
            //========================================================================================
            // 조회
            //========================================================================================
            else if (strCommend == "/조회")
            {
                if (strContents == "")
                {
                    strPrint += "[ERROR] 조회 대상이 없습니다.";
                    await Bot.SendTextMessageAsync(varMessage.Chat.Id, strPrint, ParseMode.Default, false, false, iMessageID);
                }
                else
                {
                    // Define request parameters.
                    String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                    String range = "클랜원 목록!C7:N";
                    SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);

                    ValueRange response = request.Execute();
                    if (response != null)
                    {
                        IList<IList<Object>> values = response.Values;
                        if (values != null && values.Count > 0)
                        {
                            bool bContinue = false;
                            string[] strList = new string[5];
                            int iIndex = 0;

                            foreach (var row in values)
                            {
                                if (row[0].ToString().ToUpper().Contains(strContents.ToUpper()) ||
                                    row[1].ToString().ToUpper().Contains(strContents.ToUpper()) ||
                                    row[2].ToString().ToUpper().Contains(strContents.ToUpper()))
                                {
                                    if (iIndex++ > 2)
                                    {
                                        await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 검색 결과가 너무 많습니다. (3건 초과)\n검색어를 다시 입력해주세요.", ParseMode.Default, false, false, iMessageID);
                                        return;
                                    }
                                }
                            }

                            foreach (var row in values)
                            {
                                bool isSubAccount = false;
                                bool isSearch = false;
                                string strUrl = "";
                                string battleTag = "";
                                string mainBattleTag = "";

                                if (row[0].ToString().ToUpper().Contains(strContents.ToUpper()))
                                {
                                    isSubAccount = false;
                                    isSearch = true;
                                }

                                if (row[1].ToString().ToUpper().Contains(strContents.ToUpper()))
                                {
                                    isSubAccount = false;
                                    isSearch = true;
                                }

                                if (row[2].ToString().ToUpper().Contains(strContents.ToUpper()))
                                {
                                    isSubAccount = true;
                                    isSearch = true;
                                }

                                if (isSearch == true && isSubAccount == false)
                                {
                                    string[] strBattleTag = row[1].ToString().Split('#');
                                    battleTag = strBattleTag[0] + "#" + strBattleTag[1];
                                    mainBattleTag = row[1].ToString();
                                    strUrl = "http://playoverwatch.com/ko-kr/career/pc/" + strBattleTag[0] + "-" + strBattleTag[1];
                                }
                                else if (isSearch == true && isSubAccount == true)
                                {
                                    string[] strSubAccount = row[2].ToString().Split(',');
                                    mainBattleTag = row[1].ToString();

                                    foreach (var acc in strSubAccount)
                                    {
                                        if (acc.ToString().ToUpper().Contains(strContents.ToUpper()))
                                        {
                                            string[] strBattleTag = acc.ToString().Split('#');
                                            battleTag = strBattleTag[0] + "#" + strBattleTag[1];
                                            strUrl = "http://playoverwatch.com/ko-kr/career/pc/" + strBattleTag[0] + "-" + strBattleTag[1];
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    continue;
                                }

                                string strScore = "전적을 조회할 수 없습니다.";
                                string strTier = "전적을 조회할 수 없습니다.";

                                await Bot.SendTextMessageAsync(varMessage.Chat.Id, "'" + battleTag + "'의 전적을 조회 중입니다.\n잠시만 기다려주세요.", ParseMode.Default, false, false, iMessageID);

                                try
                                {
                                    WebClient wc = new WebClient();
                                    wc.Encoding = Encoding.UTF8;

                                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                                    ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

                                    string html = wc.DownloadString(strUrl);
                                    HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                                    doc.LoadHtml(html);

                                    strScore = doc.DocumentNode.SelectSingleNode("//div[@class='competitive-rank']").InnerText;

                                    if (Int32.Parse(strScore) >= 0 && Int32.Parse(strScore) < 1500)
                                    {
                                        strTier = "브론즈";
                                    }
                                    else if (Int32.Parse(strScore) >= 1500 && Int32.Parse(strScore) < 2000)
                                    {
                                        strTier = "실버";
                                    }
                                    else if (Int32.Parse(strScore) >= 2000 && Int32.Parse(strScore) < 2500)
                                    {
                                        strTier = "골드";
                                    }
                                    else if (Int32.Parse(strScore) >= 2500 && Int32.Parse(strScore) < 3000)
                                    {
                                        strTier = "플래티넘";
                                    }
                                    else if (Int32.Parse(strScore) >= 3000 && Int32.Parse(strScore) < 3500)
                                    {
                                        strTier = "다이아";
                                    }
                                    else if (Int32.Parse(strScore) >= 3500 && Int32.Parse(strScore) < 4000)
                                    {
                                        strTier = "마스터";
                                    }
                                    else if (Int32.Parse(strScore) >= 4000 && Int32.Parse(strScore) <= 5000)
                                    {
                                        strTier = "그랜드마스터";
                                    }
                                }
                                catch
                                {
                                    await Bot.SendTextMessageAsync(varMessage.Chat.Id, "'" + battleTag + "'의 전적을 조회할 수 없습니다.", ParseMode.Default, false, false, iMessageID);
                                }

                                if (bContinue == true)
                                {
                                    strPrint += "==================================\n";
                                }

                                strPrint += "* 티어 및 점수는 전적을 조회합니다. *\n\n";
                                strPrint += "[ " + row[0].ToString() + " ]\n";
                                strPrint += "- 조회 배틀태그 : " + battleTag + "\n";
                                strPrint += "- 티어 : " + strTier + "\n";
                                strPrint += "- 점수 : " + strScore + "\n";
                                strPrint += "- 본 계정 배틀태그 : " + mainBattleTag + "\n";
                                strPrint += "- 부 계정 배틀태그 : " + row[2].ToString() + "\n";
                                strPrint += "- 포지션 : " + row[3].ToString() + "\n";
                                strPrint += "- 모스트 : " + row[4].ToString() + " / " + row[5].ToString() + " / " + row[6].ToString() + "\n";
                                strPrint += "- 이외 가능 픽 : " + row[7].ToString() + "\n";
                                strPrint += "- 접속 시간대 : " + row[8].ToString() + "\n";
                                strPrint += "- 소개 : " + row[9].ToString() + "\n";

                                bContinue = true;   // 한 명만 출력된다면 이 부분은 무시됨.
                            }
                        }
                    }

                    if (strPrint != "")
                    {
                        await Bot.SendTextMessageAsync(varMessage.Chat.Id, strPrint, ParseMode.Default, false, false, iMessageID);
                    }
                    else
                    {
                        await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 클랜원을 찾을 수 없습니다.", ParseMode.Default, false, false, iMessageID);
                    }
                }
            }
            //========================================================================================
            // 영상
            //========================================================================================
            else if (strCommend == "/영상")
            {
                // Define request parameters.
                String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                String range = "경기 URL!B5:G";
                SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);

                if (strContents == "")
                {
                    ValueRange response = request.Execute();
                    if (response != null)
                    {
                        IList<IList<Object>> values = response.Values;
                        if (values != null && values.Count > 0)
                        {
                            foreach (var row in values)
                            {
                                if (row.Count() == 6 && row[0].ToString() != "")
                                {
                                    strPrint += "[" + row[0].ToString() + "] " + row[1].ToString() + "\n";
                                }
                            }
                        }
                    }

                    strPrint += "\n/영상 날짜로 영상 주소를 조회하실 수 있습니다.\n";
                    strPrint += "(ex: /영상 181006)";
                }
                else
                {
                    string year = "20" + strContents.Substring(0, 2);
                    string month = strContents.Substring(2, 2);
                    string day = strContents.Substring(4, 2);
                    string date = year + "." + month + "." + day;
                    bool bContinue = false;
                    string user = "";

                    ValueRange response = request.Execute();
                    if (response != null)
                    {
                        IList<IList<Object>> values = response.Values;
                        if (values != null && values.Count > 0)
                        {
                            foreach (var row in values)
                            {
                                if (row.Count >= 6)
                                {
                                    if (row[0].ToString() == date)
                                    {
                                        bContinue = true;
                                    }
                                    else if (row[0].ToString() != "" && bContinue == true)
                                    {
                                        bContinue = false;
                                    }

                                    if (bContinue == true)
                                    {
                                        if (row[1].ToString() != "")
                                        {
                                            strPrint += "[ " + row[1].ToString() + " ]" + "\n";
                                        }

                                        if (row[3].ToString() == "")
                                        {
                                            if (row[4].ToString() != "")
                                            {
                                                strPrint += user + " (" + row[4].ToString() + ")" + " : " + row[5].ToString() + "\n";
                                            }
                                            else
                                            {
                                                strPrint += user + " : " + row[5].ToString() + "\n";
                                            }

                                        }
                                        else
                                        {
                                            if (row[4].ToString() != "")
                                            {
                                                strPrint += row[3].ToString() + " (" + row[4].ToString() + ")" + " : " + row[5].ToString() + "\n";
                                            }
                                            else
                                            {
                                                strPrint += row[3].ToString() + " : " + row[5].ToString() + "\n";
                                            }

                                            user = row[3].ToString();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (strPrint != "")
                {
                    const string video = @"Function/Video.jpg";
                    var fileName = video.Split(Path.DirectorySeparatorChar).Last();
                    var fileStream = new FileStream(video, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await Bot.SendPhotoAsync(varMessage.Chat.Id, fileStream, strPrint, ParseMode.Default, false, iMessageID);
                }
                else
                {
                    await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 영상을 찾을 수 없습니다.", ParseMode.Default, false, false, iMessageID);
                }
            }
            //========================================================================================
            // 검색
            //========================================================================================
            else if (strCommend == "/검색")
            {
                if (strContents == "")
                {
                    strPrint += "[ERROR] 검색 조건이 없습니다.";
                    await Bot.SendTextMessageAsync(varMessage.Chat.Id, strPrint, ParseMode.Default, false, false, iMessageID);
                }
                else
                {
                    // Define request parameters.
                    String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                    String range = "클랜원 목록!C7:N";
                    SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);
                    bool bResult = false;

                    ValueRange response = request.Execute();
                    if (response != null)
                    {
                        IList<IList<Object>> values = response.Values;
                        if (values != null && values.Count > 0)
                        {
                            strPrint += "[ '" + strContents + "' 검색 결과 ]\n";

                            foreach (var row in values)
                            {
                                if (strContents == "힐" || strContents == "딜" || strContents == "탱" || strContents == "플렉스")
                                {
                                    if (row[3].ToString().ToUpper().Contains(strContents.ToUpper()) ||
                                    row[4].ToString().ToUpper().Contains(strContents.ToUpper()) ||
                                    row[5].ToString().ToUpper().Contains(strContents.ToUpper()) ||
                                    row[6].ToString().ToUpper().Contains(strContents.ToUpper()) ||
                                    row[3].ToString() == "플렉스")
                                    {
                                        strPrint += row[0] + "(" + row[1] + ") : ";
                                        strPrint += row[3] + "(" + row[4].ToString() + "/" + row[5].ToString() + "/" + row[6].ToString() + ")\n";
                                        bResult = true;
                                    }
                                }
                                else
                                {
                                    if (row[3].ToString().ToUpper().Contains(strContents.ToUpper()) ||
                                    row[4].ToString().ToUpper().Contains(strContents.ToUpper()) ||
                                    row[5].ToString().ToUpper().Contains(strContents.ToUpper()) ||
                                    row[6].ToString().ToUpper().Contains(strContents.ToUpper()))
                                    {
                                        strPrint += row[0] + "(" + row[1] + ") : ";
                                        strPrint += row[3] + " (" + row[4].ToString() + "/" + row[5].ToString() + "/" + row[6].ToString() + ")\n";
                                        bResult = true;
                                    }
                                }
                            }
                        }
                    }

                    if (bResult == true)
                    {
                        await Bot.SendTextMessageAsync(varMessage.Chat.Id, strPrint, ParseMode.Default, false, false, iMessageID);
                    }
                    else
                    {
                        await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 검색 결과가 없습니다.", ParseMode.Default, false, false, iMessageID);
                    }
                }
            }
            //========================================================================================
            // 모임
            //========================================================================================
            else if (strCommend == "/모임")
            {
                // Define request parameters.
                String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                String range = "모임!C4:R12";
                SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);

                ValueRange response = request.Execute();
                if (response != null)
                {
                    IList<IList<Object>> values = response.Values;
                    if (values != null && values.Count > 0)
                    {
                        // 모임이름 ~ 문의
                        foreach (var row in values)
                        {
                            if (row.Count > 0)
                            {
                                if (row[0].ToString() == "프로그램" && row[1].ToString() != "")
                                {
                                    strPrint += "* " + row[0].ToString() + "\n";
                                    strPrint += "          - [" + row[1].ToString() + "] " + row[2].ToString() + " / " + row[3].ToString() + " / " + row[5].ToString() + "\n";
                                }
                                else if (row[0].ToString() == "" && row[1].ToString() != "")
                                {
                                    strPrint += "          - [" + row[1].ToString() + "] " + row[2].ToString() + " / " + row[3].ToString() + " / " + row[5].ToString() + "\n";
                                }
                                else if (row[0].ToString() != "" && row[1].ToString() != "")
                                {
                                    strPrint += "* " + row[0].ToString() + " : " + row[1].ToString() + "\n";
                                }
                                else
                                {
                                    strPrint = "[SYSTEM] 현재 예정된 모임이 없습니다.";
                                    const string meeting = @"Function/Meeting.jpg";
                                    var fileName = meeting.Split(Path.DirectorySeparatorChar).Last();
                                    var fileStream = new FileStream(meeting, FileMode.Open, FileAccess.Read, FileShare.Read);
                                    await Bot.SendPhotoAsync(varMessage.Chat.Id, fileStream, strPrint, ParseMode.Default, false, iMessageID);
                                    return;
                                }
                            }
                        }

                        // 공지, 회비
                        foreach (var row in values)
                        {
                            if (row.Count > 0)
                            {
                                if (row[6].ToString() == "공지" && row[7].ToString() != "")
                                {
                                    strPrint += "* " + row[6].ToString() + "\n";
                                    strPrint += row[7].ToString() + "\n";
                                }
                                else if (row[6].ToString() == "회비" && row[7].ToString() != "")
                                {
                                    strPrint += "* " + row[6].ToString() + "\n";
                                    strPrint += "          - [" + row[7].ToString() + "] " + row[8].ToString() + "\n";
                                }
                                else if (row[6].ToString() == "" && row[7].ToString() != "")
                                {
                                    strPrint += "          - [" + row[7].ToString() + "] " + row[8].ToString() + "\n";
                                }
                                else if (row[6].ToString() != "" && row[7].ToString() != "")
                                {
                                    strPrint += "* " + row[6].ToString() + " : " + row[7].ToString() + "\n";
                                }
                            }
                        }
                    }
                }

                List<string> lstConfirm = new List<string>();
                List<string> lstUndefine = new List<string>();

                // Define request parameters.
                range = "모임!C16:O";
                request = service.Spreadsheets.Values.Get(spreadsheetId, range);

                response = request.Execute();
                if (response != null)
                {
                    IList<IList<Object>> values = response.Values;
                    if (values != null && values.Count > 0)
                    {
                        foreach (var row in values)
                        {
                            if (row.Count != 0)
                            {
                                if (row.Count >= 13)
                                {
                                    if (row[12].ToString().ToUpper().Contains('O'))
                                    {
                                        string strConfirm = row[0].ToString();

                                        if (strConfirm != "")
                                        {
                                            lstConfirm.Add(strConfirm);
                                        }
                                    }
                                    else if (row[12].ToString().ToUpper().Contains('X'))
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        string strUndefine = row[0].ToString();
                                        if (strUndefine != "")
                                        {
                                            lstUndefine.Add(strUndefine);
                                        }
                                    }
                                }
                                else
                                {
                                    string strUndefine = row[0].ToString();
                                    if (strUndefine != "")
                                    {
                                        lstUndefine.Add(strUndefine);
                                    }
                                }
                            }
                        }
                    }

                    strPrint += "----------------------------------------\n";
                    strPrint += "★ 참가자\n";
                    strPrint += "- 확정 : ";
                    bool bFirst = true;

                    if (lstConfirm.Count == 0)
                    {
                        strPrint += "없음";
                    }
                    else
                    {
                        foreach (string confirm in lstConfirm)
                        {
                            if (bFirst == true)
                            {
                                strPrint += confirm;
                                bFirst = false;
                            }
                            else
                            {
                                strPrint += " , " + confirm;
                            }
                        }
                    }

                    strPrint += "\n- 미정 : ";
                    bFirst = true;

                    if (lstUndefine.Count == 0)
                    {
                        strPrint += "없음";
                    }
                    else
                    {
                        foreach (string undefine in lstUndefine)
                        {
                            if (bFirst == true)
                            {
                                strPrint += undefine;
                                bFirst = false;
                            }
                            else
                            {
                                strPrint += " , " + undefine;
                            }
                        }
                    }

                    strPrint += "\n----------------------------------------\n";
                    strPrint += "- 확정 : " + lstConfirm.Count + "명 / 미정 : " + lstUndefine.Count + "명 / 총 " + (lstConfirm.Count + lstUndefine.Count) + "명\n";
                    strPrint += "----------------------------------------";
                }

                if (strPrint != "")
                {
                    const string meeting = @"Function/Meeting.jpg";
                    var fileName = meeting.Split(Path.DirectorySeparatorChar).Last();
                    var fileStream = new FileStream(meeting, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await Bot.SendPhotoAsync(varMessage.Chat.Id, fileStream, strPrint, ParseMode.Default, false, iMessageID);
                }
                else
                {
                    await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 모임이 등록되지 않았습니다.", ParseMode.Default, false, false, iMessageID);
                }
            }
            else if (strCommend == "/참가")
            {
                string strNickName = strFirstName + strLastName;
                int iCellIndex = 16;
                int iTempCount = 0;
                int iRealCount = 0;
                int iBlankCell = 0;
                bool isConfirm = false;
                bool isJoin = false;

                // Define request parameters.
                String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                String range = "모임!C" + iCellIndex + ":C";
                SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);

                ValueRange response = request.Execute();
                if (response != null)
                {
                    IList<IList<Object>> values = response.Values;
                    if (values != null && values.Count > 0)
                    {
                        foreach (var row in values)
                        {
                            if (row.Count == 0)
                            {
                                if (iBlankCell == 0)
                                {
                                    iBlankCell = iTempCount;
                                }

                                iTempCount++;

                                continue;
                            }
                            else
                            {
                                if (row[0].ToString() == strNickName)
                                {
                                    iRealCount = iTempCount;
                                    isJoin = true;

                                    if (strContents == "확정")
                                    {
                                        isConfirm = true;
                                    }
                                }

                                iTempCount++;
                            }
                        }
                    }

                    if (isJoin == true && isConfirm == false)
                    {
                        await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 이미 모임에 참가신청을 했습니다.", ParseMode.Default, false, false, iMessageID);
                        return;
                    }

                    if (isConfirm == false)
                    {
                        if (iBlankCell == 0)
                        {
                            range = "모임!C" + (iCellIndex + iRealCount) + ":C";
                        }
                        else
                        {
                            range = "모임!C" + (iCellIndex + iBlankCell) + ":C";
                        }

                        // Define request parameters.
                        ValueRange valueRange = new ValueRange();
                        valueRange.MajorDimension = "COLUMNS"; //"ROWS";//COLUMNS 

                        var oblist = new List<object>() { strNickName };
                        valueRange.Values = new List<IList<object>> { oblist };

                        SpreadsheetsResource.ValuesResource.UpdateRequest updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, range);

                        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                        UpdateValuesResponse updateResponse = updateRequest.Execute();

                        if (updateResponse == null)
                        {
                            strPrint += "[ERROR] 시트를 업데이트 할 수 없습니다.";
                        }
                        else
                        {
                            if (strContents != "확정")
                            {
                                strPrint += "[SUCCESS] 참가 신청을 완료 했습니다.";
                            }
                        }
                    }

                    if (strContents == "확정")
                    {
                        if (iBlankCell == 0)
                        {
                            range = "모임!O" + (iCellIndex + iRealCount) + ":O";
                        }
                        else
                        {
                            range = "모임!O" + (iCellIndex + iBlankCell) + ":O";
                        }

                        // Define request parameters.
                        ValueRange valueRange = new ValueRange();
                        valueRange.MajorDimension = "COLUMNS"; //"ROWS";//COLUMNS 

                        var oblist = new List<object>() { "O" };
                        valueRange.Values = new List<IList<object>> { oblist };

                        SpreadsheetsResource.ValuesResource.UpdateRequest updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, range);

                        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                        UpdateValuesResponse updateResponse = updateRequest.Execute();

                        if (updateResponse == null)
                        {
                            strPrint += "\n[ERROR] 참가 확정을 할 수 없습니다.";
                        }
                        else
                        {
                            strPrint += "\n[SUCCESS] 참가 확정을 완료 했습니다.";
                        }
                    }
                }

                if (strPrint != "")
                {
                    await Bot.SendTextMessageAsync(varMessage.Chat.Id, strPrint, ParseMode.Default, false, false, iMessageID);
                }
                else
                {
                    await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 시트를 업데이트 할 수 없습니다.", ParseMode.Default, false, false, iMessageID);
                }
            }
            else if (strCommend == "/불참")
            {
                string strNickName = strFirstName + strLastName;
                int iCellIndex = 16;
                int iTempCount = 0;
                bool isJoin = false;

                // Define request parameters.
                String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                String range = "모임!C" + iCellIndex + ":C";
                SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);

                ValueRange response = request.Execute();
                if (response != null)
                {
                    IList<IList<Object>> values = response.Values;
                    if (values != null && values.Count > 0)
                    {
                        foreach (var row in values)
                        {
                            if (row.Count != 0)
                            {
                                if (row[0].ToString() == strNickName)
                                {
                                    isJoin = true;
                                    break;
                                }
                            }

                            iTempCount++;
                        }
                    }

                    if (isJoin == true)
                    {
                        range = "모임!C" + (iCellIndex + iTempCount);

                        // Define request parameters.
                        ValueRange valueRange = new ValueRange();
                        valueRange.MajorDimension = "ROWS"; //"ROWS";//COLUMNS 

                        var oblist = new List<object>() { "", "", "", "", "", "", "", "", "", "", "", "", "", "" };
                        valueRange.Values = new List<IList<object>> { oblist };

                        SpreadsheetsResource.ValuesResource.UpdateRequest updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, range);

                        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                        UpdateValuesResponse updateResponse = updateRequest.Execute();
                        if (updateResponse == null)
                        {
                            strPrint += "[ERROR] 시트를 업데이트 할 수 없습니다.";
                        }
                        else
                        {
                            strPrint += "[SUCCESS] 참가 신청을 취소 했습니다.";
                        }
                    }
                    else
                    {
                        strPrint += "[ERROR] 참가 신청을 하지 않았습니다.";
                    }
                }

                if (strPrint != "")
                {
                    await Bot.SendTextMessageAsync(varMessage.Chat.Id, strPrint, ParseMode.Default, false, false, iMessageID);
                }
                else
                {
                    await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 시트를 업데이트 할 수 없습니다.", ParseMode.Default, false, false, iMessageID);
                }
            }
            //========================================================================================
            // 투표
            //========================================================================================
            else if (strCommend == "/투표")
            {
                bool isAnonymous = false;

                // Define request parameters.
                String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                String range = "투표!B4:J";
                SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);

                ValueRange response = request.Execute();
                if (response != null)
                {
                    IList<IList<Object>> values = response.Values;
                    if (values != null && values.Count > 0)
                    {
                        CVoteDirector voteDirector = new CVoteDirector();

                        // 익명 여부
                        var value = values[0];
                        if (value[3].ToString() != "")
                        {
                            isAnonymous = true;
                        }

                        // 투표 내용
                        value = values[1];
                        string voteContents = value[0].ToString();
                        voteDirector.setVoteContents(voteContents);

                        if (voteContents != "")
                        {
                            // 투표 항목
                            value = values[11];
                            int index = 0;
                            int itemCount = 0;
                            foreach (var row in value)
                            {
                                string item = row.ToString();
                                if (item != "")
                                {
                                    CVoteItem voteItem = new CVoteItem();
                                    voteItem.AddItem(item);

                                    voteDirector.AddItem(voteItem);
                                    itemCount++;
                                }
                            }

                            // 투표자
                            index = 14;
                            int roofCount = index + itemCount;
                            for (; index < roofCount; index++)
                            {
                                value = values[index];

                                for (int i = 0; i < value.Count - 1; i++)
                                {
                                    if (value[i + 1].ToString() != "")
                                    {
                                        voteDirector.AddVoter(i, value[i + 1].ToString());
                                    }
                                }
                            }

                            // 순위
                            index = 1;
                            for (int i = 4; index <= 8; index++)
                            {
                                value = values[index];

                                if (value[i + 1].ToString() != "")
                                {
                                    CVoteRanking ranking = new CVoteRanking();

                                    if ( (value[i].ToString() == "1") || (value[i].ToString() == "2") || (value[i].ToString() == "3") || (value[i].ToString() == "4") ||
                                        (value[i].ToString() == "5") || (value[i].ToString() == "6") || (value[i].ToString() == "7") || (value[i].ToString() == "8") )
                                    {
                                        ranking.setRanking(Convert.ToInt32(value[i].ToString()), value[i + 1].ToString(), value[i + 2].ToString(), Convert.ToInt32(value[i + 3].ToString()), value[i + 4].ToString());
                                        voteDirector.AddRanking(ranking);
                                    }
                                    else
                                    {
                                        ranking.setRanking(0, value[i + 1].ToString(), value[i + 2].ToString(), Convert.ToInt32(value[i + 3].ToString()), value[i + 4].ToString());
                                        voteDirector.AddRanking(ranking);
                                    }
                                }
                            }

                            if (strContents == "")
                            {
                                strPrint += voteDirector.getVoteContents() + "\n";
                                strPrint += "=============================\n";
                                for (int i = 0; i < voteDirector.GetItemCount(); i++)
                                {
                                    strPrint += i + 1 + ". " + voteDirector.GetItem(i).getItem() + "\n";
                                }
                                strPrint += "\n \"/투표 숫자\"로 투표해주세요.";
                            }
                            else if (strContents == "결과")
                            {
                                strPrint += voteDirector.getVoteContents() + "\n";
                                strPrint += "=============================\n";
                                for (int i = 0; i < voteDirector.getRanking().Count; i++)
                                {
                                    var ranking = voteDirector.getRanking().ElementAt(i);
                                    strPrint += ranking.getRanking().ToString() + "위. " + ranking.getNumber() + " " + ranking.getVoteItem() + " [ " + ranking.getVoteCount() + "표 ] - " + ranking.getVoteRate() + "\n";
                                }
                            }
                            else
                            {
                                // 투표 했는지 체크
                                if (isAnonymous == false)
                                {
                                    // 익명 투표가 아닐 경우에만 시트로 체크
                                    for (int i = 0; i < voteDirector.GetItemCount(); i++)
                                    {
                                        List<string> voterCheckList = voteDirector.getVoter(i);

                                        foreach (var item in voterCheckList)
                                        {
                                            if (item == (strFirstName + strLastName))
                                            {
                                                await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 이미 투표를 하셨습니다.", ParseMode.Default, false, false, iMessageID);
                                                return;
                                            }
                                        }
                                    }
                                }
                                else // 익명 투표일 경우 파일에서 유저의 키로 중복 체크
                                {
                                    string[] voters = System.IO.File.ReadAllLines(@"_Voter.txt");
                                    foreach (string voter in voters)
                                    {
                                        if (voter.ToString() == senderKey.ToString())
                                        {
                                            await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 이미 투표를 하셨습니다.", ParseMode.Default, false, false, iMessageID);
                                            return;
                                        }
                                    }
                                }

                                string cellChar = "";

                                switch (strContents)
                                {
                                    case "1":
                                        cellChar = "C";
                                        break;
                                    case "2":
                                        cellChar = "D";
                                        break;
                                    case "3":
                                        cellChar = "E";
                                        break;
                                    case "4":
                                        cellChar = "F";
                                        break;
                                    case "5":
                                        cellChar = "G";
                                        break;
                                    case "6":
                                        cellChar = "H";
                                        break;
                                    case "7":
                                        cellChar = "I";
                                        break;
                                    case "8":
                                        cellChar = "J";
                                        break;
                                    default:
                                        {
                                            await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 투표 항목을 잘못 선택하셨습니다.", ParseMode.Default, false, false, iMessageID);
                                            return;
                                        }
                                }

                                int voteIndex = Convert.ToInt32(strContents);
                                if ( (voteIndex <= 0) || (voteIndex > voteDirector.GetItemCount()) )
                                {
                                    await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 투표 항목을 잘못 선택하셨습니다.", ParseMode.Default, false, false, iMessageID);
                                    return;
                                }

                                List<string> voterList = voteDirector.getVoter(voteIndex - 1);
                                int voterCount = voterList.Count;
                                string updateRange = "투표!" + cellChar + (18 + voterCount) + ":" + cellChar;

                                // Define request parameters.
                                SpreadsheetsResource.ValuesResource.GetRequest updateRequest = service.Spreadsheets.Values.Get(spreadsheetId, updateRange);
                                ValueRange valueRange = new ValueRange();
                                valueRange.MajorDimension = "COLUMNS"; //"ROWS";//COLUMNS 

                                string updateString = "";
                                if (isAnonymous == false)
                                {
                                    // 실명투표일 경우 대화명 입력
                                    updateString = strFirstName + strLastName;
                                }
                                else
                                {
                                    // 익명투표일 경우 O표시만
                                    updateString = "O";
                                }

                                var oblist = new List<object>() { updateString };
                                valueRange.Values = new List<IList<object>> { oblist };

                                SpreadsheetsResource.ValuesResource.UpdateRequest releaseRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, updateRange);

                                releaseRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                                UpdateValuesResponse releaseResponse = releaseRequest.Execute();
                                if (releaseResponse == null)
                                {
                                    strPrint = "[ERROR] 시트를 업데이트 할 수 없습니다.";
                                }
                                else
                                {
                                    await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[SUCCESS] 투표를 완료했습니다.", ParseMode.Default, false, false, iMessageID);
                                }

                                if (isAnonymous == true)
                                {
                                    System.IO.File.AppendAllText(@"_Voter.txt", senderKey.ToString() + "\n", Encoding.Default);
                                }

                                return;
                            }
                        }

                        if (strPrint != "")
                        {
                            const string vote = @"Function/Vote.jpg";
                            var fileName = vote.Split(Path.DirectorySeparatorChar).Last();
                            var fileStream = new FileStream(vote, FileMode.Open, FileAccess.Read, FileShare.Read);
                            await Bot.SendPhotoAsync(varMessage.Chat.Id, fileStream, strPrint, ParseMode.Default, false, iMessageID);
                        }
                        else
                        {
                            await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 현재 투표가 없습니다.", ParseMode.Default, false, false, iMessageID);
                        }
                    }
                }
            }
            //========================================================================================
            // 명예의 전당
            //========================================================================================
            else if (strCommend == "/기록")
            {
                if (strContents == "")
                {
                    // 내부 대회
                    String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                    String range = "명예의 전당!B7:F16";
                    SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);

                    ValueRange response = request.Execute();
                    if (response != null)
                    {
                        IList<IList<Object>> values = response.Values;
                        if (values != null && values.Count > 0)
                        {
                            int index = 1;
                            strPrint += "★ CDT 오버워치 리그 우승팀 ★\n==============================\n";

                            foreach (var row in values)
                            {
                                if (row.Count <= 0)
                                {
                                    break;
                                }

                                strPrint += "[ 1-" + index++ + " ] " + row[0].ToString() + " <" + row[1].ToString() + ">\n";
                            }
                        }
                    }

                    // 외부 대회
                    range = "명예의 전당!B21:F30";
                    request = service.Spreadsheets.Values.Get(spreadsheetId, range);

                    response = request.Execute();
                    if (response != null)
                    {
                        IList<IList<Object>> values = response.Values;
                        if (values != null && values.Count > 0)
                        {
                            int index = 1;
                            strPrint += "\n★ 외부 대회 출전 ★\n==============================\n";

                            foreach (var row in values)
                            {
                                if (row.Count <= 0)
                                {
                                    break;
                                }

                                strPrint += "[ 2-" + index++ + " ] " + row[0].ToString() + " <" + row[1].ToString() + ">\n";
                            }
                        }
                    }

                    if (strPrint != "")
                    {
                        strPrint += "\n/기록 숫자 로 조회할 수 있습니다.\n(ex: /기록 2-3)";

                        const string record = @"Function/Record.jpg";
                        var fileName = record.Split(Path.DirectorySeparatorChar).Last();
                        var fileStream = new FileStream(record, FileMode.Open, FileAccess.Read, FileShare.Read);
                        await Bot.SendPhotoAsync(varMessage.Chat.Id, fileStream, strPrint, ParseMode.Default, false, iMessageID);
                    }
                    else
                    {
                        await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 명예의 전당이 비어있습니다.", ParseMode.Default, false, false, iMessageID);
                    }
                }
                else
                {
                    string[] category = strContents.ToString().Split('-');
                    if (category.Count() <= 0)
                    {
                        await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 항목을 잘못 입력했습니다.", ParseMode.Default, false, false, iMessageID);
                        return;
                    }

                    // 내부 or 외부
                    string upper = category[0].ToUpper();
                    if (upper.Length > 1)
                    {
                        await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 항목을 잘못 입력했습니다.", ParseMode.Default, false, false, iMessageID);
                        return;
                    }

                    // 내부 대회
                    if (upper == "1")
                    {
                        // 항목
                        string strItem = category[1].ToString();
                        int item = Convert.ToInt32(strItem);
                        if (item < 1 || item > 999)
                        {
                            await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 항목을 잘못 입력했습니다.", ParseMode.Default, false, false, iMessageID);
                            return;
                        }

                        item--;

                        String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                        String range = "명예의 전당!B" + (7 + item) + ":F" + (7 + item);
                        SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);

                        ValueRange response = request.Execute();
                        if (response != null)
                        {
                            IList<IList<Object>> values = response.Values;
                            if (values != null && values.Count > 0)
                            {
                                strPrint += "★ CDT 오버워치 리그 우승팀 ★\n==============================\n";

                                var row = values[0];
                                string member = row[4].ToString().Replace("/", ",");

                                strPrint += "▷ " + row[0].ToString() + " 우승팀 [ " + row[2].ToString() + " ]\n";
                                strPrint += "* 팀장 : " + row[3].ToString() + "\n";
                                strPrint += "* 팀원 : " + member;
                            }
                        }
                    }
                    // 외부 대회
                    else if (upper == "2")
                    {
                        // 항목
                        string strItem = category[1].ToString();
                        int item = Convert.ToInt32(strItem);
                        if (item < 1 || item > 999)
                        {
                            await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 항목을 잘못 입력했습니다.", ParseMode.Default, false, false, iMessageID);
                            return;
                        }

                        item--;

                        String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                        String range = "명예의 전당!B" + (21 + item) + ":F" + (21 + item);
                        SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);

                        ValueRange response = request.Execute();
                        if (response != null)
                        {
                            IList<IList<Object>> values = response.Values;
                            if (values != null && values.Count > 0)
                            {
                                strPrint += "★ 외부 대회 출전 ★\n==============================\n";

                                var row = values[0];
                                string member = row[4].ToString().Replace("/", ",");

                                strPrint += "▷ " + row[0].ToString() + " [ " + row[2].ToString() + " ]\n";
                                strPrint += "* 팀장 : " + row[3].ToString() + "\n";
                                strPrint += "* 팀원 : " + member;
                            }
                        }
                    }

                    if (strPrint != "")
                    {
                        const string record = @"Function/Record.jpg";
                        var fileName = record.Split(Path.DirectorySeparatorChar).Last();
                        var fileStream = new FileStream(record, FileMode.Open, FileAccess.Read, FileShare.Read);
                        await Bot.SendPhotoAsync(varMessage.Chat.Id, fileStream, strPrint, ParseMode.Default, false, iMessageID);
                    }
                    else
                    {
                        await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 명예의 전당이 비어있습니다.", ParseMode.Default, false, false, iMessageID);
                    }
                }
            }
            //========================================================================================
            // 스크림
            //========================================================================================
            else if (strCommend == "/스크림")
            {
                if (strContents == "")
                {
                    String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                    String range = "스크림!B2:U17";
                    SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);

                    ValueRange response = request.Execute();
                    if (response != null)
                    {
                        IList<IList<Object>> values = response.Values;
                        if (values != null && values.Count > 0)
                        {
                            // 스크림 이름
                            var title = values[0];
                            if (title.Count > 0 && title[0].ToString() != "")
                            {
                                strPrint += "[ " + title[0].ToString() + " ]\n============================\n";

                                int index = 4;
                                for (int i = index; i < 15; i++)
                                {
                                    var row = values[i];

                                    if (row.Count <= 1)
                                    {
                                        continue;
                                    }

                                    string battleTag = row[1].ToString();
                                    string tier = row[2].ToString();
                                    string score = row[3].ToString();
                                    string position = row[5].ToString();
                                    string date = "";
                                    if (row.Count > 13 && row[13].ToString() == "O")
                                        date += "월 ";
                                    if (row.Count > 14 && row[14].ToString() == "O")
                                        date += "화 ";
                                    if (row.Count > 15 && row[15].ToString() == "O")
                                        date += "수 ";
                                    if (row.Count > 16 && row[16].ToString() == "O")
                                        date += "목 ";
                                    if (row.Count > 17 && row[17].ToString() == "O")
                                        date += "금 ";
                                    if (row.Count > 18 && row[18].ToString() == "O")
                                        date += "토 ";
                                    if (row.Count > 19 && row[19].ToString() == "O")
                                        date += "일";

                                    strPrint += "- " + battleTag.ToString() + " (" + position.ToString() + ") / " + score.ToString() + " - " + date.ToString() + "\n";
                                }
                            }
                        }
                    }

                    if (strPrint != "")
                    {
                        strPrint += "\n스크림 신청은 /스크림 [요일] 로 해주세요.\n(ex: /스크림 토일)\n신청 후 재신청을 하면 덮어씌워지므로\n날짜를 추가하려면 기존 날짜와\n합해서 신청해주세요.\n(ex: /스크림 토일, /스크림 목금토일)";

                        const string scrim = @"Function/Scrim.png";
                        var fileName = scrim.Split(Path.DirectorySeparatorChar).Last();
                        var fileStream = new FileStream(scrim, FileMode.Open, FileAccess.Read, FileShare.Read);
                        await Bot.SendPhotoAsync(varMessage.Chat.Id, fileStream, strPrint, ParseMode.Default, false, iMessageID);
                    }
                    else
                    {
                        await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 현재 모집 중인 스크림이 없습니다.", ParseMode.Default, false, false, iMessageID);
                    }
                }
                else // 일정을 입력했을 경우
                {
                    // 타이틀 Load
                    {
                        String sheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                        String titleRange = "스크림!B2:B";
                        SpreadsheetsResource.ValuesResource.GetRequest titleRequest = service.Spreadsheets.Values.Get(sheetId, titleRange);

                        ValueRange titleResponse = titleRequest.Execute();
                        if (titleResponse != null)
                        {
                            IList<IList<Object>> values = titleResponse.Values;
                            if (values != null && values.Count > 0)
                            {
                                // 스크림 이름
                                var title = values[0];
                                if (title.Count == 0 || title[0].ToString() == "")
                                {
                                    await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 현재 모집 중인 스크림이 없습니다.", ParseMode.Default, false, false, iMessageID);
                                    return;
                                }
                            }
                        }
                    }

                    int size = strContents.Length;
                    string[] day = {"", "", "", "", "", "", ""};
                    bool isConfirmDay = false;
                    bool isCancel = false;

                    if (strContents == "취소")
                    {
                        isCancel = true;
                    }
                    else
                    {
                        if (strContents.Contains("요") == true)
                        {
                            await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 잘못된 날짜입니다.", ParseMode.Default, false, false, iMessageID);
                            return;
                        }

                        // 가능 날짜 추출
                        for (int i = 0; i < size; i++)
                        {
                            string inputDay = strContents.Substring(i, 1);

                            if (inputDay == "월")
                            {
                                day[0] = "O";
                                isConfirmDay = true;
                            }
                            if (inputDay == "화")
                            {
                                day[1] = "O";
                                isConfirmDay = true;
                            }
                            if (inputDay == "수")
                            {
                                day[2] = "O";
                                isConfirmDay = true;
                            }
                            if (inputDay == "목")
                            {
                                day[3] = "O";
                                isConfirmDay = true;
                            }
                            if (inputDay == "금")
                            {
                                day[4] = "O";
                                isConfirmDay = true;
                            }
                            if (inputDay == "토")
                            {
                                day[5] = "O";
                                isConfirmDay = true;
                            }
                            if (inputDay == "일")
                            {
                                day[6] = "O";
                                isConfirmDay = true;
                            }
                        }

                        if (isConfirmDay == false)
                        {
                            await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 잘못된 날짜입니다.", ParseMode.Default, false, false, iMessageID);
                            return;
                        }
                    }
                    
                    String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                    CUser user = new CUser();

                    // 클랜원 목록에서 정보 추출
                    String range = "클랜원 목록!C7:N";
                    SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);
                    ValueRange response = request.Execute();
                    if (response != null)
                    {
                        IList<IList<Object>> values = response.Values;
                        if (values != null && values.Count > 0)
                        {
                            foreach (var row in values)
                            {
                                if (row[10].ToString() == "")
                                    continue;

                                // 유저키 일치
                                if (Convert.ToInt64(row[10].ToString()) == senderKey)
                                {
                                    user = setUserInfo(row, senderKey);
                                    break;
                                }
                            }
                        }
                    }

                    int index = 0;
                    bool isInput = false;

                    range = "스크림!C6:C";
                    request = service.Spreadsheets.Values.Get(spreadsheetId, range);
                    response = request.Execute();
                    if (response != null)
                    {
                        IList<IList<Object>> values = response.Values;
                        if (values != null && values.Count > 0)
                        {
                            int count = 0;
                            bool isSearch = false;

                            foreach (var row in values)
                            {
                                if (row.Count > 0 && row[0].ToString() != "")
                                {
                                    if (row[0].ToString() == user.MainBattleTag)
                                    {
                                        isSearch = true;
                                        index = count;
                                        break;
                                    }

                                    count++;
                                }
                                else
                                {
                                    if (isInput == false)
                                    {
                                        index = count;
                                        isInput = true;
                                    }
                                }
                            }

                            if (isCancel == true && isSearch == false)
                            {
                                await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 스크림 신청을 하지 않았습니다.", ParseMode.Default, false, false, iMessageID);
                                return;
                            }
                        }
                    }

                    // 유저키 등록이 되어있다면
                    if (user.UserKey > 0)
                    {
                        // 스크림 신청
                        if (isCancel == false)
                        {
                            string position = "";

                            if (user.Position.HasFlag(POSITION.POSITION_FLEX) == true)
                            {
                                position = "플렉스";
                            }
                            else
                            {
                                if (user.Position.HasFlag(POSITION.POSITION_DPS) == true)
                                    position += "딜";
                                if (user.Position.HasFlag(POSITION.POSITION_TANK) == true)
                                    position += "탱";
                                if (user.Position.HasFlag(POSITION.POSITION_SUPP) == true)
                                    position += "힐";
                            }

                            await Bot.SendTextMessageAsync(varMessage.Chat.Id, "'" + user.MainBattleTag + "'의 전적을 조회 중입니다.\n잠시만 기다려주세요.", ParseMode.Default, false, false, iMessageID);

                            Tuple<int, string> retTuple = referenceScore(user.MainBattleTag);
                            int score = retTuple.Item1;     // 점수
                            string tier = retTuple.Item2;   // 티어

                            if (score == 0)
                            {
                                await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 전적을 조회 할 수 없습니다.\n시트에 직접 입력해주세요.\n(원인 : 프로필 비공개, 친구공개, 미배치)", ParseMode.Default, false, false, iMessageID);
                            }

                            // Define request parameters.
                            range = "스크림!C" + (6 + index) + ":U" + (6 + index);
                            ValueRange valueRange = new ValueRange();
                            valueRange.MajorDimension = "ROWS"; //"ROWS";//COLUMNS 

                            var oblist = new List<object>()
                            {
                                user.MainBattleTag, // 배틀태그
                                tier,               // 티어
                                score,              // 점수
                                "",                 // 6명 체크
                                position,           // 포지션
                                user.MostPick[0],   // 모스트1
                                user.MostPick[1],   // 모스트2
                                user.MostPick[2],   // 모스트3
                                user.OtherPick,     // 이외 가능 픽
                                "",
                                "",
                                "",
                                day[0],             // 월
                                day[1],             // 화
                                day[2],             // 수
                                day[3],             // 목
                                day[4],             // 금
                                day[5],             // 토
                                day[6]              // 일
                            };
                            valueRange.Values = new List<IList<object>> { oblist };

                            SpreadsheetsResource.ValuesResource.UpdateRequest updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, range);

                            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                            UpdateValuesResponse updateResponse = updateRequest.Execute();
                            if (updateResponse == null)
                            {
                                strPrint += "[ERROR] 시트를 업데이트 할 수 없습니다.";
                            }
                            else
                            {
                                strPrint += "[SYSTEM] 스크림 신청이 완료 됐습니다.";
                            }
                        }
                        else
                        {
                            // 스크림 취소
                            // Define request parameters.
                            range = "스크림!C" + (6 + index) + ":Z" + (6 + index);
                            ValueRange valueRange = new ValueRange();
                            valueRange.MajorDimension = "ROWS"; //"ROWS";//COLUMNS 

                            var oblist = new List<object>() {"","","","","","","","","","","","","","","","","","","","",""};
                            valueRange.Values = new List<IList<object>> { oblist };

                            SpreadsheetsResource.ValuesResource.UpdateRequest updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, range);

                            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                            UpdateValuesResponse updateResponse = updateRequest.Execute();
                            if (updateResponse == null)
                            {
                                strPrint += "[ERROR] 시트를 업데이트 할 수 없습니다.";
                            }
                            else
                            {
                                strPrint += "[SYSTEM] 스크림 신청을 취소했습니다.";
                            }
                        }
                    }
                    else
                    {
                        strPrint += "[ERROR] 유저 정보를 업데이트 할 수 없습니다.";
                    }

                    if (strPrint != "")
                    {
                        await Bot.SendTextMessageAsync(varMessage.Chat.Id, strPrint, ParseMode.Default, false, false, iMessageID);
                    }
                }
            }
            //========================================================================================
            // 일정 조사
            //========================================================================================
            else if (strCommend == "/조사")
            {
                if (strContents == "")
                {
                    String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                    String range = "일정 조사!L5:R12";
                    SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);
                    ValueRange response = request.Execute();
                    if (response != null)
                    {
                        IList<IList<Object>> values = response.Values;
                        if (values != null && values.Count > 0)
                        {
                            var title = values[0];
                            if (title.Count == 0)
                            {
                                strPrint += "[ERROR] 현재 조사 중인 일정이 없습니다.";
                                await Bot.SendTextMessageAsync(varMessage.Chat.Id, strPrint, ParseMode.Default, false, false, iMessageID);
                                return;
                            }
                            else
                            {
                                strPrint += title[0].ToString() + "\n============================\n";

                                var day = values[6];
                                var count = values[7];

                                for (int i=0; i<7; i++)
                                {
                                    strPrint += "- " + day[i].ToString() + " : " + count[i].ToString() + "명\n";
                                }
                            }
                        }
                    }

                    if (strPrint != "")
                    {
                        strPrint += "\n조사에 참여하려면 /조사 [요일] 로 참여해주세요.\n(ex: /조사 금토일)";

                        const string calendar_research = @"Function/calendar_research.jpg";
                        var fileName = calendar_research.Split(Path.DirectorySeparatorChar).Last();
                        var fileStream = new FileStream(calendar_research, FileMode.Open, FileAccess.Read, FileShare.Read);
                        await Bot.SendPhotoAsync(varMessage.Chat.Id, fileStream, strPrint, ParseMode.Default, false, iMessageID);
                    }
                    else
                    {
                        await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 현재 모집 중인 스크림이 없습니다.", ParseMode.Default, false, false, iMessageID);
                    }
                }
                else
                {
                    int size = strContents.Length;
                    string[] day = { "", "", "", "", "", "", "" };
                    bool isConfirmDay = false;
                    bool isCancel = false;

                    if (strContents == "취소")
                    {
                        isCancel = true;
                    }
                    else
                    {
                        if (strContents.Contains("요") == true)
                        {
                            await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 잘못된 날짜입니다.", ParseMode.Default, false, false, iMessageID);
                            return;
                        }

                        // 가능 날짜 추출
                        for (int i = 0; i < size; i++)
                        {
                            string inputDay = strContents.Substring(i, 1);

                            if (inputDay == "월")
                            {
                                day[0] = "O";
                                isConfirmDay = true;
                            }
                            if (inputDay == "화")
                            {
                                day[1] = "O";
                                isConfirmDay = true;
                            }
                            if (inputDay == "수")
                            {
                                day[2] = "O";
                                isConfirmDay = true;
                            }
                            if (inputDay == "목")
                            {
                                day[3] = "O";
                                isConfirmDay = true;
                            }
                            if (inputDay == "금")
                            {
                                day[4] = "O";
                                isConfirmDay = true;
                            }
                            if (inputDay == "토")
                            {
                                day[5] = "O";
                                isConfirmDay = true;
                            }
                            if (inputDay == "일")
                            {
                                day[6] = "O";
                                isConfirmDay = true;
                            }
                        }

                        if (isConfirmDay == false)
                        {
                            await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 잘못된 날짜입니다.", ParseMode.Default, false, false, iMessageID);
                            return;
                        }
                    }

                    string calTitle = "";

                    String spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                    String range = "일정 조사!L5:L";
                    SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, range);
                    ValueRange response = request.Execute();
                    if (response != null)
                    {
                        IList<IList<Object>> values = response.Values;
                        if (values != null && values.Count > 0)
                        {
                            var title = values[0];
                            if (title.Count == 0)
                            {
                                strPrint += "[ERROR] 현재 조사 중인 일정이 없습니다.";
                                await Bot.SendTextMessageAsync(varMessage.Chat.Id, strPrint, ParseMode.Default, false, false, iMessageID);
                                return;
                            }
                            else
                            {
                                // 일정 조사 제목
                                calTitle = title[0].ToString();
                            }
                        }
                    }

                    int index = 0;

                    spreadsheetId = "17G2eOb0WH5P__qFOthhqJ487ShjCtvJ6GpiUZ_mr5B8";
                    range = "일정 조사!B5:J74";
                    request = service.Spreadsheets.Values.Get(spreadsheetId, range);
                    response = request.Execute();
                    if (response != null)
                    {
                        IList<IList<Object>> values = response.Values;
                        if (values != null && values.Count > 0)
                        {
                            int count = 0;
                            bool isInput = false;
                            bool isBlank = false;

                            foreach (var row in values)
                            {
                                if (row.Count > 1 && row[1].ToString() != "")
                                {
                                    if (row[1].ToString() == strUserName)
                                    {
                                        index = count;
                                        isInput = true;
                                        break;
                                    }
                                    else
                                    {
                                        count++;
                                    }
                                }
                                else
                                {
                                    if (index == 0 && isBlank == false)
                                    {
                                        index = count++;
                                        isBlank = true;
                                    }
                                }
                            }

                            if (isInput == false  && isBlank == false && index == 0)
                            {
                                index = count;
                            }
                        }
                    }

                    if (isCancel == false)
                    {
                        range = "일정 조사!C" + (5 + index) + ":J" + (5 + index);
                        ValueRange valueRange = new ValueRange();
                        valueRange.MajorDimension = "ROWS"; //"ROWS";//COLUMNS 

                        var oblist = new List<object>()
                            {
                                strUserName,
                                day[0],             // 월
                                day[1],             // 화
                                day[2],             // 수
                                day[3],             // 목
                                day[4],             // 금
                                day[5],             // 토
                                day[6]              // 일
                            };
                        valueRange.Values = new List<IList<object>> { oblist };

                        SpreadsheetsResource.ValuesResource.UpdateRequest updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, range);

                        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                        UpdateValuesResponse updateResponse = updateRequest.Execute();
                        if (updateResponse == null)
                        {
                            strPrint += "[ERROR] 시트를 업데이트 할 수 없습니다.";
                        }
                        else
                        {
                            strPrint += "[SYSTEM] 일정 조사를 완료했습니다.";
                        }
                    }
                    else
                    {
                        range = "일정 조사!C" + (5 + index) + ":J" + (5 + index);
                        ValueRange valueRange = new ValueRange();
                        valueRange.MajorDimension = "ROWS"; //"ROWS";//COLUMNS 

                        var oblist = new List<object>() {"","","","","","","",""};
                        valueRange.Values = new List<IList<object>> { oblist };

                        SpreadsheetsResource.ValuesResource.UpdateRequest updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, range);

                        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                        UpdateValuesResponse updateResponse = updateRequest.Execute();
                        if (updateResponse == null)
                        {
                            strPrint += "[ERROR] 시트를 업데이트 할 수 없습니다.";
                        }
                        else
                        {
                            strPrint += "[SYSTEM] 일정 조사를 취소했습니다.";
                        }
                    }

                    if (strPrint != "")
                    {
                        await Bot.SendTextMessageAsync(varMessage.Chat.Id, strPrint, ParseMode.Default, false, false, iMessageID);
                    }
                    else
                    {
                        await Bot.SendTextMessageAsync(varMessage.Chat.Id, "[ERROR] 일정 조사를 할 수 없습니다.", ParseMode.Default, false, false, iMessageID);
                    }
                }
            }
            //========================================================================================
            // 안내
            //========================================================================================
            else if (strCommend == "/안내")
            {
                await Bot.SendChatActionAsync(varMessage.Chat.Id, ChatAction.UploadPhoto);

                const string strCDTInfo01 = @"CDT_Info/01.jpg";
                const string strCDTInfo02 = @"CDT_Info/02.jpg";
                const string strCDTInfo03 = @"CDT_Info/03.jpg";
                const string strCDTInfo04 = @"CDT_Info/04.jpg";

                var fileName01 = strCDTInfo01.Split(Path.DirectorySeparatorChar).Last();
                var fileName02 = strCDTInfo02.Split(Path.DirectorySeparatorChar).Last();
                var fileName03 = strCDTInfo03.Split(Path.DirectorySeparatorChar).Last();
                var fileName04 = strCDTInfo04.Split(Path.DirectorySeparatorChar).Last();

                var fileStream01 = new FileStream(strCDTInfo01, FileMode.Open, FileAccess.Read, FileShare.Read);
                var fileStream02 = new FileStream(strCDTInfo02, FileMode.Open, FileAccess.Read, FileShare.Read);
                var fileStream03 = new FileStream(strCDTInfo03, FileMode.Open, FileAccess.Read, FileShare.Read);
                var fileStream04 = new FileStream(strCDTInfo04, FileMode.Open, FileAccess.Read, FileShare.Read);

                await Bot.SendPhotoAsync(varMessage.Chat.Id, fileStream01, "");
                await Bot.SendPhotoAsync(varMessage.Chat.Id, fileStream02, "");
                await Bot.SendPhotoAsync(varMessage.Chat.Id, fileStream03, "");
                await Bot.SendPhotoAsync(varMessage.Chat.Id, fileStream04, "");

                strPrint = "위 가이드는 본방에서 /안내 입력 시 다시 보실 수 있습니다.";

                await Bot.SendTextMessageAsync(varMessage.Chat.Id, strPrint);
            }
            //========================================================================================
            // 리포트
            //========================================================================================
            else if (strCommend == "/리포트")
            {
                //await Bot.SendTextMessageAsync(varMessage.Chat.Id, strPrint, ParseMode.Default, false, false, iMessageID);
            }
            //========================================================================================
            // 상태
            //========================================================================================
            else if (strCommend == "/상태")
            {
                strPrint += "Running.......\n";
                strPrint += "[System Time] " + systemInfo.GetNowTime() + "\n";
                strPrint += "[Running Time] " + systemInfo.GetRunningTime() + "\n";

                await Bot.SendTextMessageAsync(varMessage.Chat.Id, strPrint, ParseMode.Default, false, false, iMessageID);
            }

            strPrint = "";
        }
    }
}
