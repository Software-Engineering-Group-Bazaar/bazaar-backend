using System.Reflection.Metadata;
using System.Threading.Tasks;
using MarketingAnalytics.Interfaces;
using MarketingAnalytics.Models;
using MarketingAnalytics.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace MarketingAnalytics.Services
{
    // losa ideja realno, ovo je nada da korisniku zelje su poput ravni u R^8
    public class RecommenderAgent
    {
        private readonly double learingRate;
        private readonly double exploreThreshold;
        public const int featureDimension = 8;
        private readonly Random random = new Random();
        private readonly object _lock = new object();

        private readonly AdDbContext _context;
        public RecommenderAgent(AdDbContext context, double learingRate = 0.01, double exploreThreshold = 0.1)
        {
            this.learingRate = learingRate;
            this.exploreThreshold = exploreThreshold;
            _context = context;
        }
        public async Task<List<AdFeaturePair>> RecommendAsync(string userId, List<Advertisment> candidates, int N = 1)
        {
            if (!candidates.Any())
                throw new InvalidDataException("No candidates provided");
            if (random.NextDouble() < exploreThreshold)
            {
                var randAd = candidates[random.Next(candidates.Count)];
                var features = await FeatureEmbedding(userId, randAd);
                return new List<AdFeaturePair> {
                    new AdFeaturePair {
                        Ad = randAd,
                        FeatureVec =features
                        }
                    }; // fuj
            }
            var scoringTasks = candidates.Select(async ad => new
            {
                Object = ad,
                Score = await ScoreAd(ad, userId)
            }).ToList();

            // Await all tasks to complete
            var scoredObjects = await Task.WhenAll(scoringTasks);

            var gradeTasks = scoredObjects
                .OrderByDescending(x => x.Score)
                .Take(N)
                .Select(async x =>
                    new AdFeaturePair
                    {
                        Ad = x.Object,
                        FeatureVec = await FeatureEmbedding(userId, x.Object)
                    }
                );
            var final = await Task.WhenAll(gradeTasks);
            return final.ToList();

        }
        public async Task<double[]> FeatureEmbedding(string userId, Advertisment ad)
        {
            // x = [ 
            // bias
            // CijenaViewa
            // CijenaKlik
            // Broj Klikova za adId
            // Broj Klikova za userId sa ad Id
            // CijenaKonverzija
            // Broj Konverzija sa adId
            // Broj Konverzija za userId sa adId
            // ]
            // ---------
            // Mathcing aktivnost (binarno) po ova zad 2 pravim kandidat listu uz vrijeme...
            // Matching pcat (binarno)
            //
            var f = new double[featureDimension];
            f[0] = 1;
            var normAd = new Normalizator<AdDbContext, Advertisment>(_context);
            var muViewPrice = await normAd.MeanAsync(
                ad => ad.ViewPrice,
                ad => true
            );
            var sViewPrice = await normAd.StdDevAsync(
                ad => ad.ViewPrice,
                ad => true
            );
            f[1] = await normAd.ZScore(
                ad => ad.ViewPrice,
                ad => true,
                (double)ad.ViewPrice
            );
            f[2] = await normAd.ZScore(
                ad => ad.ClickPrice,
                ad => true,
                (double)ad.ClickPrice
            );
            f[3] = await normAd.ZScore(
                ad => ad.Clicks,
                ad => true, // mozda po pcatid gleadti
                (double)ad.Clicks
            );

            // f[4] netrivijalno naci klk je za svaku reklamu dao klikova iz tog niza vadim z
            f[4] = await _context.Clicks.CountAsync(c => c.UserId == userId && c.AdvertismentId == ad.Id);
            f[4] /= await _context.Clicks.CountAsync(c => c.UserId == userId);
            f[5] = await normAd.ZScore(
                ad => ad.ConversionPrice,
                ad => true,
                (double)ad.ConversionPrice
            );
            f[6] = await normAd.ZScore(
                ad => ad.Conversions,
                ad => true, // mozda po pcatid gleadti
                (double)ad.Conversions
            );

            // f[7] netrivijalno naci klk je za svaku reklamu dao konverzija iz tog niza vadim z
            f[7] = await _context.Conversions.CountAsync(c => c.UserId == userId && c.AdvertismentId == ad.Id);
            f[7] /= await _context.Conversions.CountAsync(c => c.UserId == userId);

            var normClick = new Normalizator<AdDbContext, Clicks>(_context);


            // f[4] = await normClick.ZScore(
            //     click => ,
            //     clicks => clicks.UserId == userId
            // )


            return f;
        }

        public async Task<double> ScoreAd(Advertisment ad, string userId)
        {
            return await Score(await FeatureEmbedding(userId, ad), userId);
        }
        public async Task<double> Score(double[] featureVec, string userId)
        {
            var weights = await GetWeights(userId);
            double score = weights.Zip(featureVec, (x, y) => x * y).Sum();
            return score;
        }

        public async Task<double[]> GetWeights(string userId)
        {
            var w = await _context.UserWeights.FirstOrDefaultAsync(w => w.UserId == userId);
            if (w != null)
                return w.Weights;
            return [
                    1.0, 0.05,
                    0.5, 0.6,
                    0.75, 1.0,
                    0.75, 1.0
                    ];
            // vidjeno interesuje je nike swoosh

        }

        public async Task SetWeights(string userId, double[] weights)
        {
            // sto ne opt 2 reda?
            var w = await _context.UserWeights.FirstOrDefaultAsync(w => w.UserId == userId);
            if (w == null)
            {
                await _context.AddAsync(new UserWeights
                {
                    Id = 0,
                    Weights = weights,
                    UserId = userId
                });

            }
            else
            {
                w.Weights = weights;
            }
            await _context.SaveChangesAsync();

        }

        // zelim feature poslan i nagradu ($)
        public async Task RecordRewardAsync(double[] featureVec, double reward, string userId)
        {

            double eval = await Score(featureVec, userId);
            double loss = reward - eval;
            var weights = await GetWeights(userId);
            weights = weights.Zip(featureVec, (x, y) => x - learingRate * loss * y).ToArray();
            await SetWeights(userId, weights);
        }
    }
}