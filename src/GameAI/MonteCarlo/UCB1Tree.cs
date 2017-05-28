﻿using System;
using System.Collections.Generic;
using SystemExtensions.Random;

namespace GameAI.MonteCarlo
{

    /// <summary>
    /// A method class for selecting moves in determinsitic, two-player, back-and-forth, zero-sum or zero-sum-tie games
    /// </summary>
    public static class UCB1Tree
    {
        /// <summary>
        /// The interface games must implement in order to use the Monte Carlo tree search algorithm.
        /// </summary>
        /// <typeparam name="TMove">The type of the moves in the IGame implementation.</typeparam>
        public interface IGame<TMove>
        {
            /// <summary>
            /// Returns the current player in int representation.
            /// </summary>
            int GetCurrentPlayer();
            /// <summary>
            /// Returns a deep copy of the game.
            /// </summary>
            IGame<TMove> DeepCopy();
            /// <summary>
            /// Perform the specified transition. Implementations must update the hash value.
            /// </summary>
            /// <param name="t">The transition to perform.</param>
            void DoMove(Transition<TMove> t);
            /// <summary>
            /// Perform any random move. To optimize this method, omit the use and update of the hash value.
            /// </summary>
            void DoRandomMove();
            /// <summary>
            /// Returns true if game is over, false otherwise.
            /// </summary>
            bool IsGameOver();
            /// <summary>
            /// Returns the player who won, represented as an int.
            /// </summary>
            int WhoWon();
            /// <summary>
            /// Returns all the legal transitions the current state of the game can perform.
            /// </summary>
            List<Transition<TMove>> GetLegalTransitions();
            /// <summary>
            /// Returns the hash code for the current state of the game.
            /// </summary>
            long GetHash();
        }

        /// <summary>
        /// A custom data structure for storing a move and the gamestate's resultant hash code after performing that move.
        /// </summary>
        /// <typeparam name="TMove"></typeparam>
        public class Transition<TMove>
        {
            /// <summary>
            /// The move to perform the transition.
            /// </summary>
            public TMove Move;
            /// <summary>
            /// The hash of the resulting gamestate.
            /// </summary>
            public long Hash;

            /// <summary>
            /// Returns a new Transition with the specified move to perform and a hash representing the resulting gamestate.
            /// </summary>
            /// <param name="move">The move to perform to execute the transition.</param>
            /// <param name="hash">The hash code of the resulting gamestate after performing the move.</param>
            public Transition(TMove move, long hash)
            {
                Move = move;
                Hash = hash;
            }

            /// <summary>
            /// Custom override for debugging and testing purposes.
            /// </summary>
            public override string ToString()
            {
                return "Move: " + Move.ToString() + ". Hash: " + Hash.ToString();
            }
        }

        /// <summary>
        /// Returns the best Transition discovered after performing the specified number of simulations on the game.
        /// </summary>
        /// <typeparam name="TMove">The type of the moves in the IGame implementation.</typeparam>
        /// <param name="game">The current state of the game from which to find the best move for the current player.</param>
        /// <param name="simulations">The number of simulations of the game to perform.</param>
        public static Transition<TMove> Search<TMove>(IGame<TMove> game, int simulations)
        {
            Dictionary<long, Node> tree = new Dictionary<long, Node>();
            tree.Add(game.GetHash(), new Node(game.GetCurrentPlayer()));

            List<Node> path = new List<Node>();

            IGame<TMove> copy;
            List<Transition<TMove>> allTransitions;
            List<Transition<TMove>> transitionsNoStats;

            Random rng = new Random();

            for (int i = 0; i < simulations; i++)
            {
                copy = game.DeepCopy();
                path.Clear();
                path.Add(tree[game.GetHash()]);

                while (true)
                {
                    if (copy.IsGameOver()) break;

                    allTransitions = copy.GetLegalTransitions();
                    transitionsNoStats = new List<Transition<TMove>>();
                    foreach (Transition<TMove> t in allTransitions)
                        if (!tree.ContainsKey(t.Hash))
                            transitionsNoStats.Add(t);

                    // SELECTION
                    if (transitionsNoStats.Count == 0)
                    {
                        float bestScore = float.MinValue;
                        float parentPlays = path[path.Count - 1].plays;
                        float ucb1Score;
                        int indexOfBestTransition = 0;
                        for (int j = 0; j < allTransitions.Count; j++)
                        {
                            ucb1Score = tree[allTransitions[j].Hash].UCBScoreForParent(parentPlays);
                            if (ucb1Score > bestScore)
                            {
                                bestScore = ucb1Score;
                                indexOfBestTransition = j;
                            }
                        }
                        Transition<TMove> bestTransition = allTransitions[indexOfBestTransition];
                        copy.DoMove(bestTransition);
                        path.Add(tree[bestTransition.Hash]);
                    }

                    // EXPANSION
                    else
                    {
                        copy.DoMove(transitionsNoStats.RandomItem(rng));

                        Node n = new Node(copy.GetCurrentPlayer());
                        tree.Add(copy.GetHash(), n);
                        path.Add(n);

                        break;
                    }
                }

                // ROLLOUT
                while (!copy.IsGameOver())
                    copy.DoRandomMove();

                // BACKPROP
                foreach (Node n in path)
                {
                    n.plays++;
                    if (copy.WhoWon() == n.player) n.wins++;
                }
            }

            // Simulations are over. Pick the best move, then return it
            allTransitions = game.GetLegalTransitions();
            int indexOfBestMoveFound = 0;
            float worstScoreFound = float.MaxValue;
            float score;

            Console.WriteLine("Root: plays-{0} wins-{1} plyr-{2}", tree[game.GetHash()].plays, tree[game.GetHash()].wins, tree[game.GetHash()].player);

            for (int i = 0; i < allTransitions.Count; i++)
            {
                Node n = tree[allTransitions[i].Hash];
                Console.WriteLine("Move {0}: plays-{1} wins-{2} plyr-{3}", i, n.plays, n.wins, n.player);


                // **NOTE**
                // The best move chosen is the move with gives the
                // opponent the least number of victories
                score = tree[allTransitions[i].Hash].ScoreForCurrentPlayer();
                if (score < worstScoreFound)
                {
                    worstScoreFound = score;
                    indexOfBestMoveFound = i;
                }
            }

            return allTransitions[indexOfBestMoveFound];
        }







        private static float UCB1(float childWins, float childPlays, float parentPlays)
        {
            return (childWins / childPlays) + (float)Math.Sqrt(2f * Math.Log(parentPlays) / childPlays);
        }



        private class Node
        {
            public float plays;
            public float wins;
            public int player;

            public float ScoreForCurrentPlayer()
            {
                return wins / plays;
            }

            public float UCBScoreForParent(float parentPlays)
            {   // plays - wins indicates how many winners for the opposing player (the player of the parent node)
                return UCB1(plays - wins, plays, parentPlays);
            }

            private Node() { }

            public Node(int player)
            {
                this.player = player;
                plays = 0f;
                wins = 0f;
            }
        }

    }
}