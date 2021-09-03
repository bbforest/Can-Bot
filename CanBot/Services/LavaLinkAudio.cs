using Discord;
using Discord.WebSocket;
using CanBot.Handlers;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Victoria;
using Victoria.EventArgs;
using Victoria.Enums;
using Victoria.Responses.Rest;
using NSoup.Nodes;
using NSoup;
using NSoup.Select;
using Newtonsoft.Json.Linq;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;

namespace CanBot.Services
{
    public sealed class LavaLinkAudio
    {
        private readonly LavaNode _lavaNode;

        public LavaLinkAudio(LavaNode lavaNode)
            => _lavaNode = lavaNode;

        public async Task<Embed> JoinAsync(IGuild guild, IVoiceState voiceState, ITextChannel textChannel)
        {
            if (_lavaNode.HasPlayer(guild))
            {
                return await EmbedHandler.CreateErrorEmbed("입장", "이미 음성채팅방에 들어와있어요!");
            }

            if (voiceState.VoiceChannel is null)
            {
                return await EmbedHandler.CreateErrorEmbed("입장", "입장할 방을 못 찾겠어요 :( 음성채팅에 접속해주세요!");
            }

            try
            {
                await _lavaNode.JoinAsync(voiceState.VoiceChannel, textChannel);
                return await EmbedHandler.CreateBasicEmbed("입장", $"{voiceState.VoiceChannel.Name}에 입장했어요!", Color.Blue);
            }
            catch (Exception ex)
            {
                return await EmbedHandler.CreateErrorEmbed("입장", ex.Message);
            }
        }

        /*This is ran when a user uses either the command Join or Play
            I decided to put these two commands as one, will probably change it in future. 
            Task Returns an Embed which is used in the command call.. */
        public async Task<Embed> PlayAsync(SocketGuildUser user, IGuild guild, string query)
        {
            //Check If User Is Connected To Voice Cahnnel.
            if (user.VoiceChannel == null)
            {
                return await EmbedHandler.CreateErrorEmbed("재생", "통화방에 접속하셔야 재생이 가능해요!");
            }

            //Check the guild has a player available.
            //길드에 플레이어가 있는지 확인합니다.
            if (!_lavaNode.HasPlayer(guild))
            {
                return await EmbedHandler.CreateErrorEmbed("재생", "재생할 통화방이 없어요ㅠㅠ");
            }

            try
            {
                //Get the player for that guild.
                var player = _lavaNode.GetPlayer(guild);

                //Find The Youtube Track the User requested.
                LavaTrack track;

                var search = Uri.IsWellFormedUriString(query, UriKind.Absolute) ?
                    await _lavaNode.SearchAsync(query)
                    : await _lavaNode.SearchYouTubeAsync(query);

                //If we couldn't find anything, tell the user. 만약 우리가 아무것도 찾지 못했으면, 사용자에게 말해.
                if (search.LoadStatus == LoadStatus.NoMatches)
                {
                    return await EmbedHandler.CreateErrorEmbed("재생", $"이 검색어로는 아무것도 못 해요...\n{query}.");
                }

                //Get the first track from the search results.
                //TODO: Add a 1-5 list for the user to pick from. (Like Fredboat)
                track = search.Tracks.FirstOrDefault();

                //If the Bot is already playing music, or if it is paused but still has music in the playlist, Add the requested track to the queue.
                if (player.Track != null && player.PlayerState is PlayerState.Playing || player.PlayerState is PlayerState.Paused)
                {
                    player.Queue.Enqueue(track);
                    await LoggingService.LogInformationAsync("Music", $"{track.Title} has been added to the music queue.");
                    return await EmbedHandler.CreateBasicEmbed("재생", $"노래를 예약했어요!\n{track.Title}", Color.Blue);
                }

                //Player was not playing anything, so lets play the requested track.
                //플레이어가 아무것도 재생하지 않았으므로 요청한 트랙을 재생합니다.
                await player.PlayAsync(track);
                await LoggingService.LogInformationAsync("Music", $"Bot Now Playing: {track.Title}\nUrl: {track.Url}");
                return await EmbedHandler.CreateBasicEmbed("재생", $"재생시작 : {track.Title}", Color.Blue);
                //return await EmbedHandler.CreateBasicEmbed("Music", $"Now Playing: {track.Title}\nUrl: {track.Url}", Color.Blue);
            }

            //If after all the checks we did, something still goes wrong. Tell the user about it so they can report it back to us.
            catch (Exception ex)
            {
                return await EmbedHandler.CreateErrorEmbed("재생", ex.Message);
            }

        }

        /*This is ran when a user uses the command Leave.
            Task Returns an Embed which is used in the command call. */
        public async Task<Embed> LeaveAsync(IGuild guild)
        {
            try
            {
                //Get The Player Via GuildID.
                //길드ID를 통해 플레이어를 확인
                var player = _lavaNode.GetPlayer(guild);

                //if The Player is playing, Stop it.
                //플레이어가 재생 중이면 중지합니다.
                if (player.PlayerState is PlayerState.Playing)
                {
                    await player.StopAsync();
                }

                //Leave the voice channel.
                //음성채널 퇴장
                await _lavaNode.LeaveAsync(player.VoiceChannel);

                await LoggingService.LogInformationAsync("Music", $"Bot has left.");
                return await EmbedHandler.CreateBasicEmbed("퇴장", $"쉬러갈게요. 담에 또 봐요!", Color.Blue);
            }
            //Tell the user about the error so they can report it back to us.
            catch (InvalidOperationException ex)
            {
                return await EmbedHandler.CreateErrorEmbed("퇴장", ex.Message);
            }
        }

        /*This is ran when a user uses the command List 
         *list명령 실행시
            Task Returns an Embed which is used in the command call.
        작업 명령 호출에 사용되는 embed 반환*/
        public async Task<Embed> ListAsync(IGuild guild)
        {
            try
            {
                /* Create a string builder we can use to format how we want our list to be displayed. */
                var descriptionBuilder = new StringBuilder();

                /* Get The Player and make sure it isn't null. 플레이어를 가져와 null이 아닌지 확인합니다. */
                var player = _lavaNode.GetPlayer(guild);
                if (player == null)
                    return await EmbedHandler.CreateErrorEmbed("재생목록", $"사용자를 찾을 수 없어요.\n지금 저를 사용하고 있나요? {GlobalData.Config.DefaultPrefix}도움말 을 입력해 사용법을 알아보세요.");

                if (player.PlayerState is PlayerState.Playing)
                {
                    /*If the queue count is less than 1 and the current track IS NOT null then we wont have a list to reply with.
                    대기열 수가 1 미만이고 현재 트랙이 null이 아닌 경우 회신할 목록이 없습니다.
                    In this situation we simply return an embed that displays the current track instead.
                    이 상황에서는 현재 트랙을 표시하는 임베드를 반환하기만 하면 됩니다.*/
                    if (player.Queue.Count < 1 && player.Track != null)
                    {
                        return await EmbedHandler.CreateBasicEmbed("재생목록", $"지금재생중 : [{player.Track.Title}]({player.Track.Url})\n예약된 노래가 없어요!", Color.Blue);
                    }
                    else
                    {
                        /* Now we know if we have something in the queue worth replying with, so we itterate through all the Tracks in the queue.
                         * 이제 응답할 가치가 있는 항목이 대기열에 있는지 알 수 있으므로 대기열에 있는 모든 트랙을 반복해서 살펴봅니다.
                         *  Next Add the Track title and the url however make use of Discords Markdown feature to display everything neatly.
                         *  다음으로 추적 제목 및 URL 추가. 그러나 디스코드 표시 기능을 사용하여 모든 항목을 깔끔하게 표시합니다.
                        This trackNum variable is used to display the number in which the song is in place. (Start at 2 because we're including the current song.
                        이 trackNum 변수는 곡이 있는 번호를 표시하는 데 사용됩니다. (현재 곡 포함이니까 2시부터 시작).*/
                        var trackNum = 2;
                        foreach (LavaTrack track in player.Queue)
                        {
                            descriptionBuilder.Append($"{trackNum}: [{track.Title}]({track.Url})\n");
                            //descriptionBuilder.Append($"{trackNum}: [{track.Title}]({track.Url}) - {track.Id}\n");
                            trackNum++;
                        }
                        return await EmbedHandler.CreateBasicEmbed("재생목록", $"지금재생중 : [{player.Track.Title}]({player.Track.Url}) \n{descriptionBuilder}", Color.Blue);
                    }
                }
                else
                {
                    return await EmbedHandler.CreateErrorEmbed("재생목록", "아무것도 예약되어있지 않아요.");
                }
            }
            catch (Exception ex)
            {
                return await EmbedHandler.CreateErrorEmbed("재생목록", ex.Message);
            }

        }

        /*This is ran when a user uses the command Skip 
            Task Returns an Embed which is used in the command call. */
        public async Task<Embed> SkipTrackAsync(IGuild guild)
        {
            try
            {
                var player = _lavaNode.GetPlayer(guild);
                /* Check if the player exists
                 * 플레이어가 있는지 확인합니다.*/
                if (player == null)
                    return await EmbedHandler.CreateErrorEmbed("재생목록", $"사용자를 찾을 수 없어요.\n지금 저를 사용하고 있나요? {GlobalData.Config.DefaultPrefix}도움말 을 입력해 사용법을 알아보세요.");
                /* Check The queue, if it is less than one (meaning we only have the current song available to skip) it wont allow the user to skip.
                 * 큐가 하나 미만(현재 노래만 건너뛸 수 있음)이면 사용자가 건너뛸 수 없습니다.
                User is expected to use the Stop command if they're only wanting to skip the current song.
                현재 곡만 건너뛰려는 경우 중지 명령을 사용해야 합니다.*/
                if (player.Queue.Count < 1)
                {
                    return await EmbedHandler.CreateErrorEmbed("넘기기", $"예약된 노래가 없어서 넘길 수 없어요!" +
                        $"\n노래가 듣기 싫다면 {GlobalData.Config.DefaultPrefix}퇴장 을 입력해주세요.");
                }
                else
                {
                    try
                    {
                        /* Save the current song for use after we skip it. */
                        var currentTrack = player.Track;
                        /* Skip the current song. */
                        await player.SkipAsync();
                        await LoggingService.LogInformationAsync("Music", $"Bot skipped: {currentTrack.Title}");
                        return await EmbedHandler.CreateBasicEmbed("넘기기", $"노래를 넘겼어요!\n{currentTrack.Title}", Color.Blue);
                    }
                    catch (Exception ex)
                    {
                        return await EmbedHandler.CreateErrorEmbed("넘기기", ex.Message);
                    }

                }
            }
            catch (Exception ex)
            {
                return await EmbedHandler.CreateErrorEmbed("넘기기", ex.Message);
            }
        }

        /*This is ran when a user uses the command Stop 
            Task Returns an Embed which is used in the command call. */
        public async Task<Embed> StopAsync(IGuild guild)
        {
            try
            {
                var player = _lavaNode.GetPlayer(guild);

                if (player == null)
                    return await EmbedHandler.CreateErrorEmbed("재생목록", $"사용자를 찾을 수 없어요.\n지금 저를 사용하고 있나요? {GlobalData.Config.DefaultPrefix}도움말 을 입력해 사용법을 알아보세요.");

                /* Check if the player exists, if it does, check if it is playing.
                     If it is playing, we can stop.*/
                if (player.PlayerState is PlayerState.Playing)
                {
                    await player.StopAsync();
                }

                await LoggingService.LogInformationAsync("Music", $"Bot has stopped playback.");
                return await EmbedHandler.CreateBasicEmbed("초기화", "재생을 멈추고 목록을 초기화했어요.", Color.Blue);
            }
            catch (Exception ex)
            {
                return await EmbedHandler.CreateErrorEmbed("초기화", ex.Message);
            }
        }

        /*This is ran when a user uses the command Volume 
            Task Returns a String which is used in the command call. */
        public async Task<string> SetVolumeAsync(IGuild guild, int volume)
        {
            if (volume > 150 || volume <= 0)
            {
                return $"음량은 1에서 150까지 가능해요!";
            }
            try
            {
                var player = _lavaNode.GetPlayer(guild);
                await player.UpdateVolumeAsync((ushort)volume);
                await LoggingService.LogInformationAsync("Music", $"Bot Volume set to: {volume}");
                return $"음량을 조절했어요. {volume}%";
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
        }

        /* 일시정지 및 다시 재생 - PARE로 통합됨
        public async Task<string> PauseAsync(IGuild guild)
        {
            try
            {
                var player = _lavaNode.GetPlayer(guild);
                if (!(player.PlayerState is PlayerState.Playing))
                {
                    await player.PauseAsync();
                    return $"멈출 수 있는게 없어요.";
                }

                await player.PauseAsync();
                return $"**정지:** {player.Track.Title}, 조용히 있어!";
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> ResumeAsync(IGuild guild)
        {
            try
            {
                var player = _lavaNode.GetPlayer(guild);

                if (player.PlayerState is PlayerState.Paused)
                {
                    await player.ResumeAsync();
                }

                return $"**정지해제:** {player.Track.Title}";
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
        }

        */

        public async Task<string> PARE(IGuild guild)
        {
            try
            {
                var player = _lavaNode.GetPlayer(guild);
                if (player.PlayerState is PlayerState.Paused)
                {
                    await player.ResumeAsync();
                    return $"**정지해제:** {player.Track.Title}";
                }
                else if (!(player.PlayerState is PlayerState.Playing))
                {
                    await player.PauseAsync();
                    return $"멈출 수 있는게 없어요.";
                }
                else
                {
                    await player.PauseAsync();
                    return $"**정지:** {player.Track.Title}, 조용히 있어!";
                }

            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
        }

        public async Task TrackEnded(TrackEndedEventArgs args)
        {
            if (!args.Reason.ShouldPlayNext())
            {
                return;
            }

            if (!args.Player.Queue.TryDequeue(out var queueable))
            {
                //await args.Player.TextChannel.SendMessageAsync("Playback Finished.");
                return;
            }

            if (!(queueable is LavaTrack track))
            {
                await args.Player.TextChannel.SendMessageAsync("재생목록에 있는 다음 항목이 트랙이 아니에요.");
                return;
            }

            await args.Player.PlayAsync(track);
            //await args.Player.TextChannel.SendMessageAsync(embed: await EmbedHandler.CreateBasicEmbed("재생시작", $"[{track.Title}]({track.Url})", Color.Blue));
        }
        public async Task<Embed> Help()
        {
            string p = GlobalData.Config.DefaultPrefix;
            return await EmbedHandler.CreateBasicEmbed("도움말", $"**봇 기능**\n``{p}도움말`` 봇을 사용하는 방법을 알려줘요." +
                $"\n``{p}코로나`` 현재 국내 코로나 현황을 보여줘요.\n``{p}웹툰`` 오늘 가장 인기있는 네이버 웹툰 5개를 보여줘요." +
                $"\n``{p}급식 <학교>`` 오늘의 급식을 알려줘요!" +
                $"\n\n**음악 관련**\n``{p}입장`` 통화방에 입장해요.\n``{p}퇴장`` 통화방에서 퇴장해요.\n``{p}재생 <검색어>`` 유튜브에서 검색된 노래를 재생해요. 재생중인 노래가 있다면 예약해요." +
                $"\n``{p}목록`` 예약된 목록을 보여줘요.\n``{p}정지`` 노래를 잠시 멈추거나 다시 재생해요.\n``{p}넘기기`` 다음 노래를 재생해요.\n``{p}음량 <1-150>`` 음량을 조절해요." +
                $"\n``{p}초기화`` 재생중인 노래와 목록을 초기화해요.", Color.Blue);
        }

        public async Task<Embed> Corona()
        {
            WebClient wc = new WebClient() { Encoding = Encoding.UTF8 };
            JObject json = JObject.Parse(wc.DownloadString("https://apiv2.corona-live.com/domestic-init.json"));

            //누적 확진자
            string data = Regex.Replace(json["stats"]["cases"].ToString(), @"[\s\[\]]", "");
            string[] current = data.Split(",");
            //누적 사망자
            data = Regex.Replace(json["stats"]["deaths"].ToString(), @"[\s\[\]]", "");
            string[] die = data.Split(",");
            //누적 격리해제
            data = Regex.Replace(json["stats"]["recovered"].ToString(), @"[\s\[\]]", "");
            string[] recovered = data.Split(",");
            //현재 치료중 계산
            var hos = string.Format("{0:#,###}", Convert.ToInt32(current[0]) - Convert.ToInt32(recovered[0]) - Convert.ToInt32(die[0]));
            var hos2 = string.Format("({0:+#,###;-#,###})", Convert.ToInt32(current[1]) - Convert.ToInt32(recovered[1]) - Convert.ToInt32(die[1]));
            //실시간 확진자
            var today = string.Format("{0:#,###}", Convert.ToInt32(Regex.Replace(json["statsLive"]["today"].ToString(), @"[\s\[\]]", "")));

            //3자리 콤마, 증감에 양음수 표시
            current[0] = string.Format("{0:#,###}", Convert.ToInt32(current[0]));
            current[1] = string.Format("({0:+#,###;-#,###})", Convert.ToInt32(current[1]));
            die[0] = string.Format("{0:#,###}", Convert.ToInt32(die[0]));
            die[1] = string.Format("({0:+#,###;-#,###})", Convert.ToInt32(die[1]));
            recovered[0] = string.Format("{0:#,###}", Convert.ToInt32(recovered[0]));
            recovered[1] = string.Format("({0:+#,###;-#,###})", Convert.ToInt32(recovered[1]));

            //백신접종자 스크래핑을 위해 User-Agent 설정(안 하면 API에서 거부함)
            wc.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.3; WOW64; Trident/7.0)");
            Document doc = NSoupClient.Parse(wc.DownloadString("https://nip.kdca.go.kr/irgd/cov19stats.do"));

            //3자리 콤마, 데이터 선택
            var fir_day = string.Format("{0:#,###}", Convert.ToInt32(doc.Select("firstCnt").Eq(0).Text));
            var fir_sum = string.Format("{0:#,###}", Convert.ToInt32(doc.Select("firstCnt").Eq(2).Text));
            var sec_day = string.Format("{0:#,###}", Convert.ToInt32(doc.Select("secondCnt").Eq(0).Text));
            var sec_sum = string.Format("{0:#,###}", Convert.ToInt32(doc.Select("secondCnt").Eq(2).Text));

            return await EmbedHandler.CreateBasicEmbed("실시간 국내 코로나 현황", $"오늘 실시간 : {today}명" +
                $"\n\n0시 기준\n확진환자 : {current[0]}{current[1]}\n치료중 : {hos}{hos2}\n격리해제 : {recovered[0]}{recovered[1]}" +
                $"\n사망자 : {die[0]}{die[1]}" +
                $"\n\n백신 접종 현황\n1차 접종 : {fir_sum}(+{fir_day})\n2차 접종 : {sec_sum}(+{sec_day})", Color.Blue);
        }

        public async Task<Embed> Webtoon()
        {
            Document doc = NSoupClient.Parse(new Uri("https://m.comic.naver.com/webtoon/weekday"), 5000);
            Elements datas = doc.Select("div.section_list_toon ul.list_toon li");

            string day = doc.Select("div.area_sublnb.lnb_weekday h3.blind").Text;
            string[] title = new string[5], author = new string[5], url = new string[5];
            for (int i = 0; i < 5; i++)
            {
                title[i] = datas.Eq(i).Select("div.info strong.title").Text;
                author[i] = datas.Eq(i).Select("div.info span.author").Text;
                url[i] = datas.Eq(i).Select("a").Attr("href");
            }
            return await EmbedHandler.CreateBasicEmbed($"{day} 웹툰 순위",
                $"\n1. [{title[0]} - {author[0]}](https://comic.naver.com{url[0]})" +
                $"\n2. [{title[1]} - {author[1]}](https://comic.naver.com{url[1]})" +
                $"\n3. [{title[2]} - {author[2]}](https://comic.naver.com{url[2]})" +
                $"\n4. [{title[3]} - {author[3]}](https://comic.naver.com{url[3]})" +
                $"\n5. [{title[4]} - {author[4]}](https://comic.naver.com{url[4]})", Color.Blue);
        }

        public async Task<Embed> Eat(string school_str)
        {
            Document doc = NSoupClient.Parse(new Uri($"https://open.neis.go.kr/hub/schoolInfo?KEY=fe74198d943c4019b9f1a01de4feaae7&SCHUL_NM={school_str}"), 5000);
            string edu = doc.Select("ATPT_OFCDC_SC_CODE").Text;
            string school = doc.Select("SD_SCHUL_CODE").Text;

            string date = DateTime.Now.ToString("yyyyMMdd");
            doc = NSoupClient.Parse(new Uri($"https://open.neis.go.kr/hub/mealServiceDietInfo?KEY=fe74198d943c4019b9f1a01de4feaae7&ATPT_OFCDC_SC_CODE={edu}&SD_SCHUL_CODE={school}&MLSV_YMD={date}"), 5000);
            Elements datas = doc.Select("row");
            string eat_result="";
            foreach (Element data in datas)
            {
                eat_result += $"**{data.Select("MMEAL_SC_NM").Text}**\n" +
                    $"{data.Select("DDISH_NM").Text.Replace("<br/>", "\n")}\n\n";
            }
            if (eat_result == "") return await EmbedHandler.CreateErrorEmbed("급식정보", $"{school_str}에 대한 오늘 급식정보를 찾지 못 했어요.");
            return await EmbedHandler.CreateBasicEmbed($"{doc.Select("SCHUL_NM").Eq(0).Text} 급식정보", eat_result, Color.Blue);
        }
    }
}
