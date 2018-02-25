﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UserBehavior.Objects;
using UserBehavior.Objects.Objects;

namespace UserBehavior.Parsers
{
    class UserBehaviorTransformer
    {
        private UserBehaviorDatabase db;

        public UserBehaviorTransformer(UserBehaviorDatabase database)
        {
            db = database;
        }

        /// <summary>
        /// Get a list of all users and their calculated rating on a given article
        /// </summary>
        public List<UserArticleRating> GetUserArticleRatings()
        {
            return db.UserActions
                .GroupBy(x => new { x.UserID, x.ArticleID })
                .Select(g => new UserArticleRating(g.Key.UserID, g.Key.ArticleID, GetRating(g)))
                .ToList();
        }

        /// <summary>
        /// Get a list of all users and their ratings on every article
        /// </summary>
        public UserArticleRatingsTable GetUserArticleRatingsTable()
        {
            UserArticleRatingsTable table = new UserArticleRatingsTable();
            
            table.UserIndexToID = db.Users.OrderBy(x => x.UserID).Select(x => x.UserID).Distinct().ToList();
            table.ArticleIndexToID = db.Articles.OrderBy(x => x.ArticleID).Select(x => x.ArticleID).Distinct().ToList();
            table.NumberOfTags = db.Tags.Count;

            foreach (int userId in table.UserIndexToID)
            {
                table.UserArticleRatings.Add(new UserArticleRatings(userId, table.ArticleIndexToID.Count)); // + table.NumberOfTags));
            }

            var userArticleRatingGroup = db.UserActions
                .GroupBy(x => new { x.UserID, x.ArticleID })
                .Select(g => new { g.Key.UserID, g.Key.ArticleID, Rating = GetRating(g) })
                .ToList();

            foreach (var userAction in userArticleRatingGroup)
            {
                int userIndex = table.UserIndexToID.IndexOf(userAction.UserID);
                int articleIndex = table.ArticleIndexToID.IndexOf(userAction.ArticleID);

                table.UserArticleRatings[userIndex].ArticleRatings[articleIndex] = userAction.Rating;
            }

            //List<UserActionTag> tags = GetUserTags();

            //foreach (UserActionTag tag in tags)
            //{
            //    int userIndex = table.UserIndexToID.IndexOf(tag.UserID);

            //    for (int i = 0; i < table.NumberOfTags; i++)
            //    {
            //        // The tag data starts after the article rating data
            //        int index = table.ArticleIndexToID.Count + i;

            //        table.UserArticleRatings[userIndex].ArticleRatings[index] = tag.ActionTagData[i];
            //    }
            //}

            //table.GenerateVisual().Save("table-test.bmp");

            return table;
        }

        /// <summary>
        /// Get a list of all users and the number of times they viewed articles with a specific tag
        /// </summary>
        public List<UserActionTag> GetUserTags()
        {
            List<UserActionTag> userData = new List<UserActionTag>();
            List<ArticleTag> articleTags = db.GetArticleTagLinkingTable();

            int numFeatures = db.Tags.Count;

            // Create a dataset that links every user action to a list of tags associated with the article that action was for, then 
            // group them by user, action, and tag so we can get a count of the number of times each user performed a action on an article with a specific tag
            var userActionTags = db.UserActions
                .Join(articleTags, u => u.ArticleID, t => t.ArticleID, (u, t) => new { u.UserID, t.TagName })
                .GroupBy(x => new { x.UserID, x.TagName })
                .Select(g => new { g.Key.UserID, g.Key.TagName, Count = g.Count() })
                .OrderBy(x => x.UserID).ThenBy(x => x.TagName)
                .ToList();

            int totalUserActions = userActionTags.Count();
            int lastFoundIndex = 0;

            // Write action-tag data
            // Parallel.ForEach(db.Users, user =>
            foreach (User user in db.Users)
            {
                int dataCol = 0;
                UserActionTag uat = new UserActionTag(user.UserID, numFeatures);

                foreach (Tag tag in db.Tags)
                {
                    // Count the number of times this user interacted with an article with this tag
                    // We can loop through like this since the list is sorted
                    int tagActionCount = 0;
                    for (int i = lastFoundIndex; i < totalUserActions; i++)
                    {
                        if (userActionTags[i].UserID == user.UserID && userActionTags[i].TagName == tag.Name)
                        {
                            lastFoundIndex = i;
                            tagActionCount = userActionTags[i].Count;
                            break;
                        }
                    }

                    uat.ActionTagData[dataCol++] = tagActionCount;
                }

                // Normalize data to values between 0 and 5
                double upperLimit = 5.0;
                double max = uat.ActionTagData.Max();
                if (max > 0)
                {
                    for (int i = 0; i < uat.ActionTagData.Length; i++)
                    {
                        uat.ActionTagData[i] = (uat.ActionTagData[i] / max) * upperLimit;
                    }
                }

                userData.Add(uat);
            }

            return userData;
        }

        ///// <summary>
        ///// Get a list of all users with their ratings on every action-tag pair
        ///// </summary>
        //public List<UserActionTag> GetUserActionTags()
        //{
        //    List<UserActionTag> userData = new List<UserActionTag>();
        //    List<string> uniqueActions = db.UserActions.Select(x => x.Action).Distinct().OrderBy(x => x).ToList();
        //    List<ArticleTag> articleTags = db.GetArticleTagLinkingTable();

        //    // There will be a total of 122 columns (2 + numTags * numActions)
        //    int numUniqueActions = uniqueActions.Count;
        //    int numUniqueTags = db.Tags.Count;
        //    int numFeatures = numUniqueActions * numUniqueTags;

        //    // Create a dataset that links every user action to a list of tags associated with the article that action was for, then 
        //    // group them by user, action, and tag so we can get a count of the number of times each user performed a action on an article with a specific tag
        //    var userActionTags = db.UserActions
        //        .Join(articleTags, u => u.ArticleID, t => t.ArticleID, (u, t) => new { u.UserID, u.UserName, u.Action, t.TagName })
        //        .GroupBy(x => new { x.UserID, x.UserName, x.Action, x.TagName })
        //        .Select(g => new { g.Key.UserID, g.Key.UserName, g.Key.Action, g.Key.TagName, Count = g.Count() })
        //        .OrderBy(x => x.UserID).ThenBy(x => x.Action).ThenBy(x => x.TagName)
        //        .ToList();

        //    int totalUserActions = userActionTags.Count();
        //    int lastFoundIndex = 0;

        //    // Write action-tag data
        //    // Parallel.ForEach(db.Users, user =>
        //    foreach (User user in db.Users)
        //    {
        //        int dataCol = 0;
        //        UserActionTag uat = new UserActionTag(user.UserID, numFeatures);

        //        foreach (Tag tag in db.Tags)
        //        {
        //            foreach (string action in uniqueActions)
        //            {
        //                // Count the number of times this user performed this action on an article with this tag
        //                // We can loop through like this since the list is sorted
        //                int tagActionCount = 0;
        //                for (int i = lastFoundIndex; i < totalUserActions; i++)
        //                {
        //                    if (userActionTags[i].UserID == user.UserID && userActionTags[i].Action == action && userActionTags[i].TagName == tag.Name)
        //                    {
        //                        lastFoundIndex = i;
        //                        tagActionCount = userActionTags[i].Count;
        //                        break;
        //                    }
        //                }

        //                uat.ActionTagData[dataCol++] = tagActionCount;
        //            }
        //        }

        //        // Normalize data to values between 0 and 1
        //        double max = uat.ActionTagData.Max();
        //        if (max > 0)
        //        {
        //            for (int i = 0; i < uat.ActionTagData.Length; i++)
        //            {
        //                uat.ActionTagData[i] /= max;
        //            }
        //        }

        //        userData.Add(uat);
        //    }

        //    return userData;
        //}

        /// <summary>
        /// Create a table of all articles as rows and all tags as columns
        /// </summary>
        public void WriteArticleFeaturesToFile(string file)
        {
            using (StreamWriter w = new StreamWriter(file))
            {
                // Write headers
                w.Write("articleId,articleName");
                foreach (Tag tag in db.Tags)
                {
                    w.Write("," + tag.Name.ToLower().Replace(" ", ""));
                }
                w.WriteLine();

                // Write articles
                foreach (Article article in db.Articles)
                {
                    w.Write(article.ArticleID + "," + article.Name);
                    foreach (Tag tag in db.Tags)
                    {
                        w.Write("," + article.Tags.Any(x => x.Name == tag.Name).ToString().ToLower());
                    }
                    w.WriteLine();
                }
            }
        }

        private double GetRating(IGrouping<object, UserAction> actions)
        {
            double rating;

            //if (actions.Any(x => x.Action == "DownVote"))
            //{
            //    rating = 1;
            //}
            //else
            //{
            //    if (actions.Any(x => x.Action == "View"))
            //    {
            //        rating += 2;
            //    }

            //    if (actions.Any(x => x.Action == "UpVote"))
            //    {
            //        rating += 2;
            //    }

            //    if (actions.Any(x => x.Action == "Download"))
            //    {
            //        rating += 1;
            //    }
            //}

            string lastVote = actions.LastOrDefault(x => x.Action == "DownVote" || x.Action == "UpVote")?.Action ?? "";
            int viewCount = actions.Count(x => x.Action == "View");
            bool downloaded = actions.Any(x => x.Action == "Download");

            if (lastVote == "DownVote")
            {
                rating = 0.1;
            }
            else
            {
                rating = 1.0;

                rating += lastVote == "UpVote" ? 2.0 : 0.0;
                rating += viewCount * 0.75;
                rating += downloaded ? 0.5 : 0.0;
            }


            // Guarantee that all user actions for an article result in an implied rating between 0.1 and 5.0
            rating = Math.Min(5.0, Math.Max(0.1, rating));


            //foreach (UserAction ua in actions)
            //{
            //    if (ua.Action == "DownVote")
            //    {
            //        rating = 1;
            //    }
            //    else
            //    {
            //        if (ua.Action == "View")
            //        {
            //            rating += 2;
            //        }

            //        if (ua.Action == "UpVote")
            //        {
            //            rating += 2;
            //        }

            //        if (ua.Action == "Download")
            //        {
            //            rating += 1;
            //        }
            //    }

            //    // Minimize the impact of older ratings
            //    rating *= 0.99;
            //}

            //rating += actions.Count(x => x.Action == "DownVote") * -2;
            //rating += actions.Count(x => x.Action == "View") * 1;
            //rating += actions.Count(x => x.Action == "UpVote") * 2;
            //rating += actions.Count(x => x.Action == "Download") * 1;

            //if (actions.Any(x => x.Action == "DownVote"))
            //{
            //    rating = -5;
            //}
            //else
            //{
            //    if (actions.Any(x => x.Action == "View"))
            //    {
            //        rating += 1;
            //    }

            //    if (actions.Any(x => x.Action == "UpVote"))
            //    {
            //        rating += 2;
            //    }

            //    if (actions.Any(x => x.Action == "Download"))
            //    {
            //        rating += 1;
            //    }
            //}

            return rating;
        }
    }
}
