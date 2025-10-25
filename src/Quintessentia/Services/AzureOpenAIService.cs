using Azure;
using Azure.AI.OpenAI;
using OpenAI.Audio;
using OpenAI.Chat;
using System.ClientModel;
using System.Diagnostics;
using System.Text;
using Quintessentia.Services.Contracts;

namespace Quintessentia.Services
{
    public class AzureOpenAIService : IAzureOpenAIService
    {
        private readonly ILogger<AzureOpenAIService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AzureOpenAIClient _sttClient;
        private readonly AzureOpenAIClient _gptClient;
        private readonly AzureOpenAIClient _ttsClient;
        private readonly string _sttDeployment;
        private readonly string _gptDeployment;
        private readonly string _ttsDeployment;
        
        // Azure OpenAI Whisper API has a 25MB file size limit - use smaller files to get all of the results faster
        private const long MAX_AUDIO_FILE_SIZE = 5 * 1024 * 1024; // 5MB to leave buffer
        private const int CHUNK_OVERLAP_SECONDS = 1; // Overlap to avoid losing words at boundaries

        public AzureOpenAIService(ILogger<AzureOpenAIService> logger, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;

            // Initialize Speech-to-Text client
            var sttEndpoint = _configuration["AzureOpenAI:Endpoint"]
                ?? throw new InvalidOperationException("Speech-to-Text endpoint not configured");
            var sttKey = _configuration["AzureOpenAI:Key"] 
                ?? throw new InvalidOperationException("Speech-to-Text key not configured");
            _sttDeployment = _configuration["AzureOpenAI:SpeechToText:DeploymentName"] 
                ?? throw new InvalidOperationException("Speech-to-Text deployment name not configured");
            _sttClient = new AzureOpenAIClient(new Uri(sttEndpoint), new AzureKeyCredential(sttKey));

            // Initialize GPT client
            var gptEndpoint = _configuration["AzureOpenAI:Endpoint"] 
                ?? throw new InvalidOperationException("GPT endpoint not configured");
            var gptKey = _configuration["AzureOpenAI:Key"] 
                ?? throw new InvalidOperationException("GPT key not configured");
            _gptDeployment = _configuration["AzureOpenAI:GPT:DeploymentName"] 
                ?? throw new InvalidOperationException("GPT deployment name not configured");
            _gptClient = new AzureOpenAIClient(new Uri(gptEndpoint), new AzureKeyCredential(gptKey));

            // Initialize Text-to-Speech client
            var ttsEndpoint = _configuration["AzureOpenAI:Endpoint"] 
                ?? throw new InvalidOperationException("Text-to-Speech endpoint not configured");
            var ttsKey = _configuration["AzureOpenAI:Key"] 
                ?? throw new InvalidOperationException("Text-to-Speech key not configured");
            _ttsDeployment = _configuration["AzureOpenAI:TextToSpeech:DeploymentName"] 
                ?? throw new InvalidOperationException("Text-to-Speech deployment name not configured");
            _ttsClient = new AzureOpenAIClient(new Uri(ttsEndpoint), new AzureKeyCredential(ttsKey));

            _logger.LogInformation("AzureOpenAIService initialized with deployments - STT: {STT}, GPT: {GPT}, TTS: {TTS}",
                _sttDeployment, _gptDeployment, _ttsDeployment);
        }

        private Models.AzureOpenAISettings? GetCustomSettings()
        {
            return _httpContextAccessor.HttpContext?.Items["AzureOpenAISettings"] as Models.AzureOpenAISettings;
        }

        private (AzureOpenAIClient client, string deployment) GetSTTClientAndDeployment()
        {
            var customSettings = GetCustomSettings();
            if (customSettings != null && (customSettings.Endpoint != null || customSettings.Key != null || customSettings.WhisperDeployment != null))
            {
                var endpoint = customSettings.Endpoint ?? _configuration["AzureOpenAI:Endpoint"]!;
                var key = customSettings.Key ?? _configuration["AzureOpenAI:Key"]!;
                var deployment = customSettings.WhisperDeployment ?? _sttDeployment;
                
                _logger.LogInformation("Using custom STT settings - Endpoint: {Endpoint}, Deployment: {Deployment}", 
                    endpoint, deployment);
                
                var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));
                return (client, deployment);
            }
            return (_sttClient, _sttDeployment);
        }

        private (AzureOpenAIClient client, string deployment) GetGPTClientAndDeployment()
        {
            var customSettings = GetCustomSettings();
            if (customSettings != null && (customSettings.Endpoint != null || customSettings.Key != null || customSettings.GptDeployment != null))
            {
                var endpoint = customSettings.Endpoint ?? _configuration["AzureOpenAI:Endpoint"]!;
                var key = customSettings.Key ?? _configuration["AzureOpenAI:Key"]!;
                var deployment = customSettings.GptDeployment ?? _gptDeployment;
                
                _logger.LogInformation("Using custom GPT settings - Endpoint: {Endpoint}, Deployment: {Deployment}", 
                    endpoint, deployment);
                
                var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));
                return (client, deployment);
            }
            return (_gptClient, _gptDeployment);
        }

        private (AzureOpenAIClient client, string deployment) GetTTSClientAndDeployment()
        {
            var customSettings = GetCustomSettings();
            if (customSettings != null && (customSettings.Endpoint != null || customSettings.Key != null || customSettings.TtsDeployment != null))
            {
                var endpoint = customSettings.Endpoint ?? _configuration["AzureOpenAI:Endpoint"]!;
                var key = customSettings.Key ?? _configuration["AzureOpenAI:Key"]!;
                var deployment = customSettings.TtsDeployment ?? _ttsDeployment;
                
                _logger.LogInformation("Using custom TTS settings - Endpoint: {Endpoint}, Deployment: {Deployment}", 
                    endpoint, deployment);
                
                var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));
                return (client, deployment);
            }
            return (_ttsClient, _ttsDeployment);
        }

        public async Task<string> TranscribeAudioAsync(string audioFilePath)
        {
            try
            {
                _logger.LogInformation("Starting transcription for file: {FilePath}", audioFilePath);

                if (!File.Exists(audioFilePath))
                {
                    throw new FileNotFoundException($"Audio file not found: {audioFilePath}");
                }

                var fileInfo = new FileInfo(audioFilePath);
                var fileSize = fileInfo.Length;

                _logger.LogInformation("Audio file size: {Size} bytes ({SizeMB:F2} MB)", fileSize, fileSize / (1024.0 * 1024.0));

                // Check if file exceeds size limit
                if (fileSize > MAX_AUDIO_FILE_SIZE)
                {
                    _logger.LogWarning("File size ({Size} bytes) exceeds limit ({Limit} bytes). Will process in chunks.", fileSize, MAX_AUDIO_FILE_SIZE);
                    return await TranscribeAudioInChunksAsync(audioFilePath);
                }

                // Process single file
                return await TranscribeSingleAudioFileAsync(audioFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transcribing audio file: {FilePath}", audioFilePath);
                throw;
            }
        }

        private async Task<string> TranscribeSingleAudioFileAsync(string audioFilePath)
        {
            var (client, deployment) = GetSTTClientAndDeployment();
            var audioClient = client.GetAudioClient(deployment);

            using var audioStream = File.OpenRead(audioFilePath);
            var transcriptionOptions = new AudioTranscriptionOptions
            {
                ResponseFormat = AudioTranscriptionFormat.Verbose,
                Temperature = 0.0f
            };

            var result = await audioClient.TranscribeAudioAsync(audioStream, audioFilePath, transcriptionOptions);

            var transcript = result.Value.Text;
            _logger.LogInformation("Transcription completed. Length: {Length} characters", transcript.Length);

            return transcript;
        }

        private async Task<string> TranscribeAudioInChunksAsync(string audioFilePath)
        {
            var tempChunkDirectory = Path.Combine(Path.GetTempPath(), $"audio_chunks_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempChunkDirectory);

            try
            {
                _logger.LogInformation("Chunking audio file into smaller segments...");

                // Get audio duration using FFprobe
                var duration = await GetAudioDurationAsync(audioFilePath);
                _logger.LogInformation("Audio duration: {Duration} seconds", duration);

                // Calculate chunk duration based on file size
                var fileSize = new FileInfo(audioFilePath).Length;
                var chunkDuration = (int)((MAX_AUDIO_FILE_SIZE / (double)fileSize) * duration * 0.9); // 90% of max to be safe
                
                // Ensure reasonable chunk size (at least 60 seconds, max 600 seconds)
                chunkDuration = Math.Max(60, Math.Min(chunkDuration, 600));
                
                _logger.LogInformation("Using chunk duration of {ChunkDuration} seconds", chunkDuration);

                // Split audio into chunks
                var chunkFiles = await SplitAudioIntoChunksAsync(audioFilePath, tempChunkDirectory, chunkDuration);
                _logger.LogInformation("Created {Count} audio chunks", chunkFiles.Count);


                // For debugging purposes, use a fixed transcript
                //var combinedTranscript = "This is an iHeart podcast. If your commercial building is over 20,000 square feet, Washington's clean buildings law not only probably applies to you, but goes into effect as soon as July of 2026. Puget Sound Energy's free clean buildings accelerator is here to help. You'll get step by step guidance, easy energy tracking tools, ready to use plan templates and a year of ongoing support. It's a simple way to save energy and lower costs. Sign up at pse.com forward slash clean buildings. Run a business and not thinking about podcasting. Think again. More Americans listen to podcasts than ad supported streaming music from Spotify and Pandora. And as the number one podcaster, iHeart's twice as large as the next two combined. Learn how podcasting can help your business. Call 844-844-iHeart. Hi there. It's Bender from Jody and Bender mornings on 95.7. The jet. Thanks so much for listening to us on our new and improved iHeart radio app, music, radio podcasts. Just make sure to set our show as a preset on the app because you're going to listen and you're going to love us. And then you'll have to go searching for us again on the app or make the jet a preset. That way you'll never miss anything. Time flies, Puget Sound, showdown and more. Jody and I, just like you, only we have microphones. And that makes our boss nervous. The jet on the iHeart radio app. Did you know Tide has been upgraded to provide an even better clean in cold water? Tide is specifically designed to fight any stain you throw at it. Even in cold butter. Yep. Chocolate ice cream. Sure thing. Barbecue sauce. Tide's got you covered. You don't need to use warm water. Additionally, Tide pods let you confidently fight tough stains with new cold time technology. Just remember, if it's got to be clean, it's got to be Tide. Media. All right, we're back. I'm Ed Zitron and this is Better Offline. This is the fourth and final episode of this four part series where I dissect in excruciating comprehensive detail the AI bubble and tell you why I think its implosion is inevitable. We're at the end, which is fitting, because in this episode we're going to talk about why and how this entire sham dies. Again, if you're jumping in now, start from the beginning. We talk about a lot of things and they're all kind of threads together. I know, I know you want to hear my explanation of how open AI is fucked with a capital fuck, but we've got to lay the groundwork to get there. Okay. Caught up. Good, good. We're good. We're good. You ready? We're good. You're good. Me? Yep. Okay. We'll begin. One of the comfortable lies that people tell themselves is that the AI bubble is similar to the fiber boom or the dot com boom or Uber or that we're in the growth stage or that this is what software companies do. They spend a bunch of money, then pull the profit lever. The thing is, this is nothing like anything you've ever seen before, because this is the dumbest shit the tech industry has ever, ever done. AI data centers are nothing like fiber because there are very few actual use cases for these GPUs outside of AI, and none of them are remotely hyperscale revenue drivers. As I discussed a month or so ago, data center development accounted for more of America's GDP growth than all consumer spending combined in the first half of 2025. And there really isn't any demand for AI in general, let alone at the scale that these hundreds of billions of dollars are being sunk into. The conservative estimate of capital expenditures related to data centers is around $400 billion. But given the $50 billion in a quarter in private equity invested, I'm going to guess it breaks half a trillion, all to build capacity for an industry yet to prove itself. And this whole NVIDIA OpenAI $100 billion funding news should only fill you full of dread. But also it isn't fucking final. I stopped reporting as if it's done. I swear to fucking... Anyway. Good. According to CNBC, and I quote, the initial $10 billion tranche is locked in at a $500 billion valuation and expected to close within a month or so once the transaction has been finalized with successive $10 billion rounds planned, each to be priced at the company's then current valuation as new capacity comes online. What a horrible deal. They can just raise whatever. Let me just go back to NVIDIA. But let me just be clear. OpenAI has written a lot of checks that neither it nor anyone else can cash. And for it to survive, it needs to raise more than honestly, about $500 billion and maybe another $400 billion in other people paying for stuff. It's astonishing really. And no point is anyone asking how exactly OpenAI builds the data centers to fill full of the GPUs to get the rest of NVIDIA's funding. In fact, I'm genuinely shocked and a little disgusted by how poorly this story has been told. Let's go point by point. $10 billion is not enough for OpenAI to build a data center. The 1.1, 1.2 gigawatt Abilene, Texas data center being built by Oracle and Cruiser for OpenAI required $15 billion in debt. And that's not including the $40 billion of chips needed to power it. Though I question whether all of those are going into Abilene or somewhere else we should get to later. OpenAI can also not afford to pay for everything it's promised. OpenAI will burn, they say $115 billion in the next four years, according to the information. But I believe the company intentionally leaked those numbers before the announcement of their $300 billion deal with Oracle. That's when you take into account the numbers shared by the information, which involved them burning $47 billion in 2028, a year when OpenAI is meant to make $70 billion in payments to compute to Oracle, another $28 billion to Microsoft for any other costs. There's no space for the $300 billion to go. And there's actually another $100 billion they're promising for backup compute as well. On top of all of this, OpenAI is still yet to convert to a for-profit entity and must do so by the end of 2025 or they'll lose $20 billion in funding from SoftBank. A few weeks ago, Microsoft and OpenAI co-published an announcement that said they'd signed a non-binding memorandum of understanding for the next phase of their partnership, which the media quickly took to mean a deal was done. No deal has been done. This is an announcement of nothing. On top of all of this, venture capital might run out of the current rate of investment in around six quarters and OpenAI needs more investment than ever. The information recently published some worrying data from venture capitalist John Sakoda that at today's clip, the industry would run out of money in six quarters, adding that the money wouldn't run out until the end of 2028 if it wasn't for OpenAI and Anthropic. In a very real sense, OpenAI threatens the future of available capital for the tech industry. Well, the good news is that they're going to make lots of money, right? I mean, OpenAI leaked that they'll make $200 billion in four years time. I mean, if you're concussed, I can see how you believe that. Let's go. I'm going to read you a bunch of numbers. I know, I know it's kind of going to be annoying to hear this. For the next 30 seconds, I'm going to just throw figures at you, but pay attention because it's important to realize how insane this is. So in 2025 OpenAI projects to make $13 billion and have free cashflow of negative $9 billion. In 2026, OpenAI will make $29 billion or $30 billion, but have free cashflow of negative $17 billion. In 2027, OpenAI will make $60 billion, but have negative cashflow of negative $35 billion. And in 2028, OpenAI will make $100 billion, but have free cashflow of negative $47 billion. In 2029, OpenAI will make $145 billion, but have free cashflow of negative $8 billion somehow. I just want to reiterate that OpenAI's projections see it reduce its negative cashflow by $39 billion or one and a half times 3M's revenue from the last financial year in a single year, while also increasing their revenue by a similar amount, give or take a few billion. Who's counting? Certainly not Sarah Fryer, the CFO of OpenAI. It's also fucking stupid, but don't worry. I know, I know some of you have been worried. You see big strong men with tears in their eyes saying, sir, sir, OpenAI, they're not going to have enough money. They're going to die, but don't worry. In 2030, OpenAI will make $200 billion and somehow have positive free cashflow of $38 billion. It's just that easy. I don't teach you this in business school. It's just that easy. How are they going to make it? They're going to make it. They'll be fine. OpenAI's current reported burn is $115 billion through 2030, which means there's no way that these projections that were leaked included $300 billion in compute costs. Even when you factor in the revenue, there's no space in the projections to absorb the Oracle money. And from what I can tell by 2029, OpenAI will have burned upwards of $290 billion, assuming it survives that long, which I do not believe it will. Don't worry, though. OpenAI is about to make some crazy money. I say in no way being totally sarcastic. I'm going to now read you some of the projections that CFO Sarah Fry signed off on. This is a professional chief financial officer. Again, more numbers, but pay attention, pay attention. This is, I want to give you, because these are numbers you just heard, but I want to give you some comparison points. So in 2026, OpenAI will make, according to the projections, or these are the projections, $30 billion in revenue, or just under the $34 billion that Salesforce made in 2024. In 2027, OpenAI will allegedly make $60 billion or $2.6 billion more than the $57.4 billion that Oracle made in its fiscal year 2025, or $8.4 billion more than Broadcom made in 2024. In 2028, this is crazy. OpenAI will make $100 billion, $2.3 billion more than Tesla made in 2024, or $11.66 billion more than TSMC, the single largest chip manufacturer in the world, made in 2024. In 2029, things get crazier because OpenAI will then make $154 billion or $14.5 billion more than the $130.5 billion that Nvidia made in its fiscal year 2025. 5. But in 2030, this is when they really blow the doors off, they will make $200 billion or $35.5 billion than the $164.5 billion that Meta made in 2024. Just so we are clear, OpenAI intends to 10X its revenue in the space of 4 years selling software and access to its models, in an industry with about $60 billion of revenue in 2025. How will it do this? OpenAI does not say. I don't know OpenAI CFO Sarah Fryer, but I do know that signing off on these numbers is, at the very least, ethically questionable. But putting aside the ridiculousness of OpenAI's deals or its funding requirements, Fryer has willfully allowed Sam Ullman and OpenAI to state goals that defy reality or good sense, all to take advantage of investors and public markets that have completely lost the plot. I'm actually going to be blunter. OpenAI has signed multiple different deals and contracts for amounts it cannot afford to pay, that it cannot hope to raise the money to pay for, that defy the amounts of venture capital and private capital available, all to sustain a company that will burn half a trillion dollars in the next 4 years based on their own expenditures, and they have no path to profitability of any kind. But what about that Nvidia deal, Ed? $100 billion is a lot of money, right? It is, but the announcement is bullshit. I know you may have read otherwise, but Nvidia didn't give OpenAI $100 billion. In fact, $90 billion of that is contingent on OpenAI building roughly $125 billion worth of datacenters with $200 billion worth of GPUs inside them. And those datacenters will have to be built faster than anyone could possibly build something at that scale, and there's no evidence that OpenAI knows where or when these facilities will be built or who will build them. Important detail as well, Nvidia's datacenters have nothing to do with Stargate. So when you read that OpenAI has 7 gigawatts of Stargate, whatever, it doesn't exist, who cares? That's nothing to do with Nvidia. Oracle is not building these datacenters. The 10 gigawatts that they need to build with Nvidia are something completely separate. As soon as July of 2026, Puget Sound Energy's free Clean Buildings Accelerator is here to help. You'll get step-by-step guidance, easy energy tracking tools, ready-to-use plan templates, and a year of ongoing support. It's a simple way to save energy at lower costs. Sign up at pse.com forward slash clean buildings. And as the number one podcaster, iHeart's twice as large as the next two combined. So whatever your customers listen to, they'll hear your message. Plus, only iHeart can extend your message to audiences across broadcast radio. Think podcasting can help your business? Think iHeart. Streaming, radio, and podcasting. Call 844-844-iHeart to get started. That's 844-844-iHeart. But you know what? Let's take a step back. With something really simple. Datacenters take forever to build. As I've said previously, based on current reports, it's taking Oracle and Crusoe around 2.5 years per gigawatt of datacenter capacity. And nowhere in these reports does one reporter take a second to say, hey, what datacenters are you talking about? Hey, didn't Sam Altman say back in July he was building 10 gigawatts of datacenter capacity with Oracle? But wait, now Oracle and OpenAI have done another announcement that says they're only doing 7 gigawatts, but they already had a schedule on 10 gigawatts. Like I said, totally different 10 gigawatts to the one with NVIDIA. But you know what? You've got a few hundred billion dollars of datacenters, now you're making real money. Anyway, I cannot be clear enough how unlikely it is that the first gigawatt of NVIDIA systems will be deployed in the second half of 2026, as NVIDIA has stated with their deal with OpenAI. And that's as if the land has been bought and got permits and the construction has started. None of this has happened. But you know, let's get really specific on costs. Crusoe's 1.2 gigawatts of compute for OpenAI is a $15 billion joint venture, which means a gigawatt of compute runs about $12.5 billion worth of construction. Abilene's eight buildings are meant to hold 50,000 NVIDIA GB200 GPUs and their associated networking infrastructure. So let's say a gigawatt is around 333,000 Blackwell GPUs, even though the GB200s are technically two of them put together. Though this math is a little funky due to NVIDIA promising to install its new Rubin GPUs in these theoretical datacenters, that means these datacenters will require a little under $200 billion worth of GPUs. By my maths, that's about $325 billion, but NVIDIA is saying it could be half a trillion. But you know who gives a shit, right? Numbers don't matter. You've got Jensen Huang telling CNBC that one gigawatt costs $50-60 billion, you've got Altman claiming the same thing to Bloomberg. Well, my math says something completely fucking different. But no one seems to want to bother. You know, it's only like the places I'm quoting are leading business outlets with teams of thousands of people with incredible infrastructure where they could sit down and say, well, you know, it costs this much to build Stargate Abilene. Why don't we work out actually what this costs? No, no, no, just eat the info slop. Yum, yum, yum. You know, it doesn't exhaust me. It doesn't make me cynical and frustrated with people who could do a good job. Indeed, I've seen them do a good job, but they just seem to not want to do one with this. And it genuinely bothers me on a day-to-day basis. Okay, I realize I'm just ranting at this point, but I'm going to be honest. I'm just, I'm so tired of this. As a reminder as well, OpenAI has agreed to spend $300 billion on Compute with Oracle. But at this point, as you can tell, numbers basically don't have meaning. Numbers stop meaning anything. If you believe these numbers, you're a fantasist or a liar, or you just don't want to think about them too much. Because when you think about them, you start to realize how unrealistic it is. And even if you remain steadfast in your belief of the transformative potential of large language models, eventually there comes a point where reality must prevail and you accept that we're in a bubble, whether you like it or not. What we're witnessing is one of the most egregious wastes of capital in history, sold by career charlatans with their reputations laundered by a tech and business media afraid to criticize the powerful and analysts that don't seem to want to tell their investors the truth. There are no historic comparisons here. Even Britain's abominable 1800s railway bubble, which absorbed half of the country's national income, created valuable infrastructure for trains, a vehicle that can move people to and from places. So the train doesn't randomly just go backwards when you make it go forwards. It doesn't become a bus or a dog at random. It doesn't hallucinate doors that open and close at random. Trains work. And GPUs are not trains, nor are they cars or even CPUs. They are not adaptable to many other kinds of work, nor are they the infrastructure of the future of tech. Because they're already quite old and with everybody focused on buying them, you'd absolutely see one other use case by now that actually mattered. GPUs are expensive, power hungry, environmentally destructive, and require their own kinds of cooling and server infrastructure, making every GPU data center an environmental and fiscal bubble unto themselves. And whereas the Victorian train infrastructure still exists in the UK, though it has been upgraded over the years, a GPU has a limited useful lifespan. These are cards that can and will break after a period of extended usage, whether that period is five years or later, though I hear it's one to three, and they'll inevitably be superseded by something better and more powerful, meaning that the resale value of that GPU will only go down with a price depreciation that's akin to a new car, and then a used car, and then a car that you kick the door of every so often. I am telling you, as I have been telling you for years, again and again and again, that the demand is not there for generative AI, and the demand is never ever arriving. The only reason anyone humors any of this crap is the endless hoarding of GPUs to build capacity for a revolution that will never arrive. Well, that and OpenAI, a company that's built and sold on lies about ChatGPT's capabilities. ChatGPT's popularity and OpenAI's hunger for endless amounts of compute have created the illusion of demand due to the sheer amount of capacity required to keep their services operational, all so that it can burn around $8 billion or more in 2025, and hundreds of billions of dollars more by 2030, if they make it, which they won't. The Nvidia deal is a farce, an obvious attempt by the largest company on the American stock market to prop up the one significant revenue generator in the entire industry, knowing that time is running out for it to create new avenues for eternal growth. I'd argue that Nvidia's deal also shows the complete contempt that these companies have for the media. There are no details about how this deal works beyond the initial $10 billion, there's no land purchase, no data center construction started, yet the media slurps it down without a second thought. I am but one man, and I am fucking peculiar. I did not learn financial analysis in school, but I appear to be one of the few people doing even the most basic analysis of these deals, and while I'm having a great time doing so, I'm also extremely frustrated about how little effort is being put into prying apart these deals by the people. I realize how ridiculous all of this sounds, I get it, there's so much money being promised to so many people, market rallies built off the back of these massive deals, and I get that the assumption is that this much money can't be wrong, that this many people wouldn't just say stuff without intending to follow through, or without considering whether their company could afford it. I know it's hard to conceive that these hundreds of billions of dollars could be invested in something for no apparent reason, but it's happening right goddamn now in front of your eyes, and I am going to be merciless on anyone who attempts to  ";

                // Transcribe each chunk
                var transcripts = new string[chunkFiles.Count];
                var semaphore = new SemaphoreSlim(10); // Limit to (up to) 10 concurrent transcriptions
                var tasks = new List<Task>();

                for (int i = 0; i < chunkFiles.Count; i++)
                {
                    var index = i; // Capture index for closure
                    var task = Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            _logger.LogInformation("Transcribing chunk {Current}/{Total}...", index + 1, chunkFiles.Count);
                            var chunkTranscript = await TranscribeSingleAudioFileAsync(chunkFiles[index]);
                            transcripts[index] = chunkTranscript;
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
                var combinedTranscript = string.Join(" ", transcripts);

                // Combine transcripts
                _logger.LogInformation("All chunks transcribed. Combined transcript length: {Length} characters", 
                    combinedTranscript.Length);

                return combinedTranscript;
            }
            finally
            {
                // Clean up temporary chunk files
                try
                {
                    if (Directory.Exists(tempChunkDirectory))
                    {
                        Directory.Delete(tempChunkDirectory, true);
                        _logger.LogInformation("Cleaned up temporary chunk directory");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temporary chunk directory: {Directory}", tempChunkDirectory);
                }
            }
        }

        private async Task<double> GetAudioDurationAsync(string audioFilePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{audioFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start FFprobe process");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"FFprobe failed: {error}");
            }

            if (!double.TryParse(output.Trim(), out var duration))
            {
                throw new InvalidOperationException($"Failed to parse audio duration: {output}");
            }

            return duration;
        }

        private async Task<List<string>> SplitAudioIntoChunksAsync(string audioFilePath, string outputDirectory, int chunkDurationSeconds)
        {
            var chunkFiles = new List<string>();
            var duration = await GetAudioDurationAsync(audioFilePath);
            var chunkCount = (int)Math.Ceiling(duration / chunkDurationSeconds);

            for (int i = 0; i < chunkCount; i++)
            {
                var startTime = i * chunkDurationSeconds;
                var chunkFile = Path.Combine(outputDirectory, $"chunk_{i:D3}.mp3");
                
                // Use FFmpeg to extract chunk with slight overlap
                var actualStart = Math.Max(0, startTime - (i > 0 ? CHUNK_OVERLAP_SECONDS : 0));
                var chunkDuration = chunkDurationSeconds + (i > 0 ? CHUNK_OVERLAP_SECONDS : 0);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{audioFilePath}\" -ss {actualStart} -t {chunkDuration} -acodec copy \"{chunkFile}\" -y",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start FFmpeg process");
                }

                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"FFmpeg failed to create chunk {i}: {error}");
                }

                if (File.Exists(chunkFile))
                {
                    chunkFiles.Add(chunkFile);
                    _logger.LogInformation("Created chunk {Index}: {File} (start: {Start}s)", i, chunkFile, actualStart);
                }
            }

            return chunkFiles;
        }

        public async Task<string> SummarizeTranscriptAsync(string transcript)
        {
            try
            {
                _logger.LogInformation("Starting summarization. Transcript length: {Length} characters", transcript.Length);

                var (client, deployment) = GetGPTClientAndDeployment();
                var chatClient = client.GetChatClient(deployment);

                // Comprehensive prompt designed to produce a 5-minute summary
                var systemPrompt = @"You are an expert audio summarizer specializing in creating concise, engaging audio summaries. Your summaries are designed to be read aloud and should sound natural when spoken.

Your task is to distill audio content into exactly 5 minutes worth of spoken content (approximately 750 words at 150 words per minute).

Guidelines:
1. CRITICAL: Stay within 750 words maximum. This is non-negotiable.
2. Capture ALL salient points, key insights, main arguments, and important takeaways
3. Maintain logical flow and narrative coherence
4. Use clear, conversational language appropriate for audio narration
5. Focus on substantive content; eliminate pleasantries, filler words, and tangential discussions
6. If the content is very long, prioritize the most impactful and actionable information
7. Structure your summary with a brief introduction, main content organized by themes, and a concise conclusion
8. Use transitions that work well in spoken form (e.g., 'Moving on to...', 'Another key point is...')

If the transcript is extremely long and contains more content than can fit in 750 words while preserving all key points, use a multi-pass distillation approach:
- First, identify the core themes and most critical insights
- Then, distill supporting details to their essence
- Finally, craft a coherent narrative that maximizes information density while maintaining clarity

Your summary should be ready to be converted directly to speech without any further editing.";

                var userPrompt = $@"Please summarize the following audio transcript into exactly 5 minutes of spoken content (approximately 750 words). Ensure all important points are captured while staying within the word limit.

Transcript:
{transcript}

Provide your summary below:";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                };

                var chatOptions = new ChatCompletionOptions
                {
                    Temperature = 1.0f, // Lower temperature for more focused, consistent output
                };

                // Uncomment for testing
                //var summary = "Here’s a concise, five-minute distillation of this episode of Better Offline with Ed Zitron, the final part of his series on the AI bubble—focused on why and how it collapses.\r\n\r\nIntroduction\r\nEd argues that today’s AI buildout is unlike past tech booms and is, in his words, the most reckless bet the industry has made. The core claim: there isn’t real demand to justify the hundreds of billions being sunk into AI data centers and GPUs. The financials, timelines, and physical constraints don’t add up—and the media isn’t challenging them.\r\n\r\nWhy this isn’t like fiber, dot-com, or Uber\r\n- AI data centers aren’t like fiber because GPUs have few viable use cases outside large-scale AI training and inference. There’s no broad, hyperscale alternative revenue stream.\r\n- Yet data center capex has exploded: a conservative estimate is ~$400 billion already, likely over $500 billion when including private equity, all to create capacity for an industry that hasn’t proven sustainable demand.\r\n- In the first half of 2025, data center construction contributed more to US GDP growth than all consumer spending combined—an extraordinary imbalance if end demand doesn’t materialize.\r\n\r\nThe OpenAI–Nvidia funding narrative\r\n- The headline $100 billion Nvidia “deal” is not $100 billion in cash. According to CNBC, only an initial $10 billion at a $500 billion valuation is set; further $10 billion tranches depend on OpenAI bringing new capacity online at then-current valuations. Ed calls it a terrible structure that assumes OpenAI can build massive capacity first.\r\n- The remaining $90 billion is contingent on OpenAI building roughly $125 billion worth of data centers and filling them with about $200 billion of GPUs—on a timeline faster than anyone has ever built at that scale. There’s no evidence of land, permits, or construction for these facilities.\r\n- Crucially, these Nvidia-tied data centers are separate from OpenAI’s “Stargate” and separate from Oracle’s builds, despite public confusion.\r\n\r\nOpenAI’s costs, deals, and runway problems\r\n- Example: the 1.1–1.2 gigawatt Abilene, Texas facility being built by Oracle and Crusoe for OpenAI needed $15 billion in debt—excluding roughly $40 billion in chips. That’s a hint at how expensive this really is.\r\n- OpenAI has reportedly agreed to spend $300 billion on compute with Oracle, plus another $100 billion in “backup” compute. But its leaked projections show no room for those costs.\r\n- A Microsoft–OpenAI “next phase” announcement was just a non-binding MOU—not a finalized deal.\r\n- OpenAI must convert to a for-profit by end of 2025 or lose $20 billion from SoftBank.\r\n- Venture capital could run out at the current pace in about six quarters, according to VC John Sakoda—with OpenAI and Anthropic consuming a significant share. That threatens the broader tech funding environment.\r\n\r\nThe revenue and cash burn projections Ed says defy reality\r\n- OpenAI’s internal figures reportedly show:\r\n  - 2025: $13 billion in revenue, negative $9 billion free cash flow.\r\n  - 2026: ~$30 billion revenue, negative $17 billion FCF.\r\n  - 2027: $60 billion revenue, negative $35 billion FCF.\r\n  - 2028: $100 billion revenue, negative $47 billion FCF.\r\n  - 2029: $145 billion revenue, negative $8 billion FCF.\r\n  - 2030: $200 billion revenue, positive $38 billion FCF.\r\n- The implied swing—from minus $47 billion to plus $38 billion in two years while revenues soar—is what Ed calls implausible.\r\n- The comparisons are even bolder: surpassing Salesforce by 2026, Oracle by 2027, TSMC and Tesla by 2028, Nvidia by 2029, and even Meta by 2030—while selling access to models in an industry that totaled about $60 billion in 2025. There’s no clear path explaining how.\r\n\r\nBuild times and hard constraints\r\n- Data centers take years. Oracle and Crusoe are averaging roughly 2.5 years per gigawatt today.\r\n- Nvidia has suggested the first gigawatt for OpenAI could arrive in the second half of 2026—but there’s no land, permits, or construction in place to make that plausible.\r\n- Costs are murky, and public statements don’t reconcile. Crusoe’s 1.2 gigawatt JV implies around $12.5 billion of construction per gigawatt, before GPUs. Ed’s math suggests hundreds of billions for GPUs alone for the planned scale, far below public claims of $50–60 billion per gigawatt. The numbers vary wildly, and the press isn’t reconciling them.\r\n\r\nThe bigger critique: no real demand, and GPUs aren’t railroads\r\n- Past bubbles left durable infrastructure. Victorian rail still works; it moves people and goods. GPUs aren’t that. They’re power-hungry, require specialized cooling, depreciate quickly, and have limited resale value. They’re not broadly adaptable like CPUs.\r\n- Ed’s thesis: real demand for generative AI isn’t there. The perceived demand is a mirage, created by hoarding GPUs to keep services running and to signal growth. That burns cash—an estimated $8 billion in 2025 and potentially hundreds of billions by 2030.\r\n\r\nMedia credulity and systemic risk\r\n- Ed argues Nvidia is propping up the industry’s core revenue engine while time runs out to find sustainable growth, and the media has largely accepted vague announcements without details on land, builds, or financing.\r\n- If OpenAI’s promises unravel, the consequence isn’t just one company. It risks draining the venture ecosystem, misallocating capital at historic scale, and leaving little lasting value.\r\n\r\nConclusion\r\nEd’s bottom line: the AI bubble collapses under the weight of physics, permitting, capital costs, cash burn, and absent demand. The timelines don’t work, the money doesn’t pencil, and the deals are contingent on capacity that doesn’t exist. When reality catches up—funding tightens, data centers slip, and customers don’t appear at the projected scale—the implosion follows. His call to listeners: look past the headlines, do the basic math, and demand specifics about capacity, costs, and timelines.";

                var response = await chatClient.CompleteChatAsync(messages, chatOptions);
                var summary = response.Value.Content[0].Text;

                _logger.LogInformation("Summarization completed. Summary length: {Length} characters, ~{Words} words",
                    summary.Length, summary.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
                // If summary is still too long, perform a second pass
                var wordCount = summary.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount > 800)
                {
                    _logger.LogWarning("Summary exceeded target length ({Words} words). Performing second pass compression.", wordCount);
                    summary = await CompressSummaryAsync(chatClient, summary);
                }

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error summarizing transcript");
                throw;
            }
        }

        private async Task<string> CompressSummaryAsync(ChatClient chatClient, string initialSummary)
        {
            var compressionPrompt = $@"The following summary is slightly too long for a 5-minute audio narration. Please compress it to exactly 750 words or fewer while preserving ALL key points and maintaining natural flow for speech.

Current summary:
{initialSummary}

Provide the compressed version:";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are an expert editor specializing in compressing content while preserving meaning. Reduce the following text to 750 words maximum while keeping all critical information."),
                new UserChatMessage(compressionPrompt)
            };

            // Uncomment for testing
            // var compressedSummary = "This final installment of Better Offline with Ed Zitron lays out why the current AI boom implodes: the industry is making its most reckless bet yet, pouring hundreds of billions into data centers and GPUs without real, sustainable demand. The financials, timelines, and physical constraints don’t pencil out—and the press largely isn’t pressing for details.\r\n\r\nWhy this isn’t like fiber, dot-com, or Uber\r\n- GPUs have few viable uses outside large-scale AI training and inference. Unlike fiber or cloud infrastructure, there’s no broad alternate revenue stream to backfill excess capacity.\r\n- Yet data center capex has exploded: a conservative estimate is around $400 billion already, likely over $500 billion including private equity, to build capacity for demand that hasn’t been proven.\r\n- In the first half of 2025, data center construction contributed more to US GDP growth than all consumer spending combined—an extraordinary imbalance if end demand never materializes.\r\n\r\nThe OpenAI–Nvidia funding narrative\r\n- The headline $100 billion Nvidia “deal” isn’t $100 billion in cash. CNBC reports only an initial $10 billion at a $500 billion valuation; additional $10 billion tranches depend on OpenAI bringing new capacity online at then-current valuations. Ed calls that a terrible structure that assumes OpenAI can build massive capacity first.\r\n- The remaining $90 billion is contingent on OpenAI building roughly $125 billion of data centers and buying about $200 billion of GPUs—faster than anyone has ever built at that scale. There’s no evidence of land, permits, or construction for these facilities.\r\n- These Nvidia-tied data centers are separate from OpenAI’s “Stargate” and separate from Oracle’s builds, despite public confusion.\r\n\r\nOpenAI’s costs, deals, and runway problems\r\n- The 1.1–1.2 gigawatt Abilene, Texas facility being built by Oracle and Crusoe for OpenAI needed $15 billion in debt—excluding roughly $40 billion in chips. That hints at how expensive this really is.\r\n- OpenAI has reportedly agreed to spend $300 billion on compute with Oracle, plus another $100 billion in “backup” compute—costs that don’t fit its leaked projections.\r\n- The Microsoft–OpenAI “next phase” was a non-binding MOU, not a finalized deal.\r\n- OpenAI must convert to a for-profit by end of 2025 or lose $20 billion from SoftBank.\r\n- Venture capital could run out at the current pace in about six quarters, says VC John Sakoda, with OpenAI and Anthropic consuming a significant share—threatening broader tech funding.\r\n\r\nThe revenue and cash burn projections Ed says defy reality\r\n- OpenAI’s internal figures reportedly show:\r\n  - 2025: $13 billion revenue, negative $9 billion free cash flow.\r\n  - 2026: ~$30 billion revenue, negative $17 billion FCF.\r\n  - 2027: $60 billion revenue, negative $35 billion FCF.\r\n  - 2028: $100 billion revenue, negative $47 billion FCF.\r\n  - 2029: $145 billion revenue, negative $8 billion FCF.\r\n  - 2030: $200 billion revenue, positive $38 billion FCF.\r\n- The implied swing—from minus $47 billion to plus $38 billion in two years while revenues explode—is what Ed calls implausible.\r\n- The comparisons are even bolder: surpassing Salesforce by 2026, Oracle by 2027, TSMC and Tesla by 2028, Nvidia by 2029, and Meta by 2030—while selling access to models in an industry totaling about $60 billion in 2025. There’s no clear path to that scale.\r\n\r\nBuild times and hard constraints\r\n- Data centers take years. Oracle and Crusoe are averaging roughly 2.5 years per gigawatt today.\r\n- Nvidia has suggested the first gigawatt for OpenAI could arrive in the second half of 2026—but there’s no land, permits, or construction in place to make that plausible.\r\n- Costs are murky, and public statements don’t reconcile. Crusoe’s 1.2 gigawatt JV implies around $12.5 billion of construction per gigawatt, before GPUs. Ed’s math suggests hundreds of billions for GPUs alone at the planned scale, while some public claims suggest $50–60 billion per gigawatt. The numbers vary wildly, and the press isn’t reconciling them.\r\n\r\nThe bigger critique: no real demand, and GPUs aren’t railroads\r\n- Past bubbles left durable, broadly useful infrastructure. Victorian railways still move people and goods. GPUs aren’t that: they’re power-hungry, need specialized cooling, depreciate quickly, and have limited resale value. They’re not as adaptable as CPUs.\r\n- Ed’s thesis: real demand for generative AI isn’t there. The perceived demand is a mirage created by hoarding GPUs to keep services running and to signal growth. That burns cash—an estimated $8 billion in 2025 and potentially hundreds of billions by 2030.\r\n\r\nMedia credulity and systemic risk\r\n- Ed argues Nvidia is propping up the industry’s core revenue engine while time runs out to find sustainable growth, and the media has largely accepted vague announcements without specifics on land, builds, or financing.\r\n- If OpenAI’s promises unravel, the fallout won’t be contained to one company. It risks draining the venture ecosystem, misallocating capital at historic scale, and leaving little lasting value.\r\n\r\nConclusion\r\nEd’s bottom line: the AI bubble collapses under the weight of physics, permitting, capital costs, cash burn, and absent demand. The timelines don’t work, the money doesn’t pencil, and the deals are contingent on capacity that doesn’t exist. When reality catches up—funding tightens, data centers slip, and customers don’t appear at the projected scale—the implosion follows. His call to listeners: look past the headlines, do the basic math, and demand specifics about capacity, costs, and timelines.";
            var response = await chatClient.CompleteChatAsync(messages);
            var compressedSummary = response.Value.Content[0].Text;
            _logger.LogInformation("Compression completed. New length: ~{Words} words",
                compressedSummary.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

            return compressedSummary;
        }

        public async Task<string> GenerateSpeechAsync(string text, string outputFilePath)
        {
            try
            {
                _logger.LogInformation("Starting text-to-speech generation. Text length: {Length} characters", text.Length);

                var (client, deployment) = GetTTSClientAndDeployment();
                var audioClient = client.GetAudioClient(deployment);

                // Get TTS configuration settings with override support
                var customSettings = GetCustomSettings();
                
                // Speed ratio: check custom settings, then configuration, then default to 1.0
                var speedRatio = customSettings?.TtsSpeedRatio 
                    ?? float.Parse(_configuration["AzureOpenAI:TextToSpeech:SpeedRatio"] ?? "1.0");
                
                // Response format: check custom settings, then configuration, then default to mp3
                var responseFormatString = customSettings?.TtsResponseFormat 
                    ?? _configuration["AzureOpenAI:TextToSpeech:ResponseFormat"] ?? "mp3";
                
                var responseFormat = responseFormatString.ToLowerInvariant() switch
                {
                    "mp3" => GeneratedSpeechFormat.Mp3,
                    "opus" => GeneratedSpeechFormat.Opus,
                    "aac" => GeneratedSpeechFormat.Aac,
                    "flac" => GeneratedSpeechFormat.Flac,
                    "wav" => GeneratedSpeechFormat.Wav,
                    "pcm" => GeneratedSpeechFormat.Pcm,
                    _ => GeneratedSpeechFormat.Mp3
                };

                _logger.LogInformation("Using TTS settings - SpeedRatio: {SpeedRatio}, Format: {Format}", 
                    speedRatio, responseFormatString);

                var generateOptions = new SpeechGenerationOptions
                {
                    ResponseFormat = responseFormat,
                    SpeedRatio = speedRatio
                };

                var result = await audioClient.GenerateSpeechAsync(text, GeneratedSpeechVoice.Alloy, generateOptions);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(outputFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save audio to file
                using var audioStream = result.Value.ToStream();
                using var fileStream = File.Create(outputFilePath);
                await audioStream.CopyToAsync(fileStream);

                _logger.LogInformation("Text-to-speech generation completed. File saved to: {FilePath}", outputFilePath);

                return outputFilePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating speech from text");
                throw;
            }
        }
    }
}
