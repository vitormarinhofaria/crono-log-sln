﻿using CronoLog.Models;
using CronoLog.Utils;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using NanoXLSX;
using NanoXLSX.Styles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CronoLog.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [EnableCors("TrelloHostPolicy")]
    public class WebAppController : ControllerBase
    {
        private readonly IMongoClient mDbClient;

        public WebAppController(IMongoClient mongoClient)
        {
            mDbClient = mongoClient;
        }

        [HttpGet("board/{boardId}")]
        public async Task<IActionResult> GetFullBoard(string boardId)
        {
            var boardsCollection = DatabaseUtils.BoardsCollection(mDbClient);
            var cardsCollection = DatabaseUtils.CardsCollection(mDbClient);

            var boardFilter = Builders<TrelloBoard>.Filter.Eq("Id", boardId);
            var board = await boardsCollection.Find(boardFilter).FirstOrDefaultAsync();

            if (board != null)
            {
                List<TrelloCard> cards = new();
                board.Cards.ForEach(c =>
                {
                    var cardFilter = Builders<TrelloCard>.Filter.Eq("Id", c);
                    var card = cardsCollection.Find(cardFilter).FirstOrDefault();
                    if (card != null)
                    {
                        cards.Add(card);
                    }
                });

                var response = new JsonResult(new FullBoardData { BoardId = board.Id, BoardName = board.Name, Cards = cards, Members = board.Members });
                return response;
            }
            else
            {
                return new JsonResult(new { Error = "Quadro não encontrado" })
                {
                    StatusCode = 404
                };
            }
        }
        [HttpGet("board/mini/{boardId}")]
        public async Task<IActionResult> GetBoard(string boardId)
        {
            var boardsCollection = DatabaseUtils.BoardsCollection(mDbClient);
            var boardFilter = Builders<TrelloBoard>.Filter.Eq("Id", boardId);
            var board = await boardsCollection.Find(boardFilter).FirstOrDefaultAsync();

            if (board != null)
            {
                return new JsonResult(board);
            }

            return new JsonResult("Quadro não encontrado");
        }
        [HttpGet("card/{cardId}")]
        public async Task<IActionResult> GetCard(string cardId)
        {
            var cardsCollection = DatabaseUtils.CardsCollection(mDbClient);
            var cardFilter = Builders<TrelloCard>.Filter.Eq("Id", cardId);
            var card = await cardsCollection.Find(cardFilter).FirstOrDefaultAsync();
            Console.WriteLine($"Card {card.Name} is {card.Active}");

            if (card != null)
            {
                var jResult = new JsonResult(card);
                return jResult;
            }

            return new JsonResult("Card n\uc3a3o encontrado");
        }

        [HttpDelete("chrono/{card_id}/{chrono_id}/{member_id}")]
        public async Task<IActionResult> DeleteChrono(string card_id, string chrono_id, string member_id)
        {
            var cardCollection = DatabaseUtils.CardsCollection(mDbClient);
            var cardFilter = Builders<TrelloCard>.Filter.Eq("Id", card_id);
            var card = await cardCollection.Find(cardFilter).FirstOrDefaultAsync();
            if (card != null)
            {
                var chrono = card.Timers.Find((t) => t.Id == chrono_id);
                if (chrono != null)
                {
                    //if (chrono.StartMember.Id == member_id || member_id == "5c9947e123dce225c1747f63")// Checkar membro editando o cartão é o mesmo que criou o Timer ou é o ID especifico (Henrique)
                    if (true) // pular checagem de membro
                    {
                        card.Timers.Remove(chrono);
                        await cardCollection.FindOneAndReplaceAsync(cardFilter, card);
                        var utf8string = Encoding.UTF8.GetBytes("Chrono deletado com sucesso!");
                        return new JsonResult("ascs");
                    }
                    else
                    {
                        //var utf8string = Encoding.UTF8.GetBytes("N\uc3a3o \uc3a9 permitido deletar Chronos que n\uc3a3o pertencem a voc\uc3aa");
                        //return new JsonResult("nptvc");
                    }
                }
                else
                {
                    var utf8string = Encoding.UTF8.GetBytes("Chrono n\uc3a3o encontrado");
                    return new JsonResult("tne");
                }

            }
            else
            {
                var utf8string = Encoding.UTF8.GetBytes("Cart\uc3a3o n\uc3a3o encontrado");
                return new JsonResult("cne");
            }
        }
        [HttpPost("chrono")]
        public async Task<IActionResult> UpdateChrono(UpdateChronoRequest updateData)
        {
            var cardsCollection = DatabaseUtils.CardsCollection(mDbClient);
            var cardFilter = Builders<TrelloCard>.Filter.Eq("Id", updateData.CardId);
            var card = await cardsCollection.Find(cardFilter).FirstOrDefaultAsync();

            if (card != null)
            {
                var chrono = card.Timers.Find((t) => t.Id == updateData.ChronoId);
                if (chrono != null)
                {
                    if (chrono.StartMember.Id == updateData.RequestMemberId || updateData.RequestMemberId == "5c9947e123dce225c1747f63")
                    {
                        var boardsCollection = DatabaseUtils.BoardsCollection(mDbClient);
                        var boardFilter = Builders<TrelloBoard>.Filter.Eq("Id", card.BoardId);
                        var board = await boardsCollection.Find(boardFilter).FirstOrDefaultAsync();
                        var updateMember = board.Members.Find((m) => m.Id == updateData.MemberId);

                        chrono.StartMember = updateMember;
                        chrono.Start = updateData.Start;
                        if (chrono.State != TimeState.RUNNING)
                        {
                            chrono.End = updateData.End;
                        }
                        await cardsCollection.FindOneAndReplaceAsync(cardFilter, card);

                        //return new JsonResult("Alteraç\uc3a3o salva com sucesso!");
                        return new JsonResult("ascs");
                    }
                    else
                    {
                        //return new JsonResult("N\uc3a3o \uc3a9 permitido alterar Timers que n\uc3a3o pertencem a voc\uc3aa");
                        var resJson = new JsonResult("nptvc");
                        resJson.ContentType = "application/json; charset=utf-16";
                        return resJson;
                    }
                }
                else
                {
                    //return new JsonResult("Timer n\uc3a3o encontrado");
                    return new JsonResult("tne");
                }
            }
            else
            {
                //return new JsonResult("Cart\uc3a3o n\uc3a3o encontrado");
                return new JsonResult("cne");
            }
        }

        [HttpGet("excel/{boardId}")]
        public IActionResult GetExcel(string boardId)
        {
            Stopwatch sw = new();
            sw.Start();

            var boardsCollection = DatabaseUtils.BoardsCollection(mDbClient);
            var boardFilter = Builders<TrelloBoard>.Filter.Eq("Id", boardId);
            var board = boardsCollection.Find(boardFilter).FirstOrDefault();

            var cardsCollection = DatabaseUtils.CardsCollection(mDbClient);
            var cardFilter = Builders<TrelloCard>.Filter.Eq("BoardId", boardId);
            var cards = cardsCollection.Find(cardFilter).ToList();
            cards = cards.FindAll(c =>
                c.Active && !c.CurrentList.Name.ToLower().Contains("dúvidas") && !c.CurrentList.Name.ToLower().Contains("duvidas") && !c.CurrentList.Name.ToLower().Contains("geral"));

            // Update the name of the member in the timers with the Board database entry
            Dictionary<string, TrelloMember> MembersCache = new();
            cards.ForEach(card =>
            {
                card.Timers.ForEach(timer =>
                {
                    ReplaceMemberFromBoard(board, MembersCache, timer.StartMember);
                    if (timer.EndMember is not null)
                    {
                        ReplaceMemberFromBoard(board, MembersCache, timer.EndMember);
                    }
                });
            });

            string wbName = $"Resumo - {board.Name}";
            sw.Stop();
            Console.WriteLine($"Tempo para Recuperar Dados: {sw.Elapsed}");
            sw.Reset();
            sw.Start();
            string fileName = $"{wbName}.xlsx";
            {
                Workbook workbook = new Workbook(fileName, wbName);
                FirstPage(cards, wbName, workbook);
                workbook.AddWorksheet("Detalhes");
                workbook.SetCurrentWorksheet("Detalhes");
                SecondPage(cards, board.Members, wbName, workbook, board);

                workbook.Save();
            }
            sw.Stop();
            Console.WriteLine($"Tempo para criar o XLSX: {sw.Elapsed}");

            var f = System.IO.File.Open(fileName, FileMode.Open);
            byte[] bytes = new byte[f.Length];
            f.Read(bytes, 0, Convert.ToInt32(f.Length));
            f.Close();
            System.IO.File.Delete(fileName);
            return new JsonResult(bytes);
        }

        public static void ReplaceMemberFromBoard(TrelloBoard board, Dictionary<string, TrelloMember> membersCache, TrelloMember sMember)
        {
            if (membersCache.TryGetValue(sMember.Id, out TrelloMember? found))
            {
                sMember.Name = found!.Name;
            }
            else
            {
                var m = board.Members.Find(m => m.Id == sMember.Id);
                if (m is not null)
                {
                    membersCache.Add(m.Id, m);
                    sMember.Name = m.Name;
                }
            }
        }

        public static async Task GetExcelAllBoardsDetails(IMongoClient db)
        {
            var boards = await DatabaseUtils.BoardsCollection(db).FindAsync(Builders<TrelloBoard>.Filter.Empty);
            var boardsList = await boards.ToListAsync();

            string fileName = "wwwroot/Resumo-Torres.xlsx";
            System.IO.File.Delete(fileName);
            Workbook workbook = new(fileName, "Detalhes");

            workbook.CurrentWorksheet.SetColumnWidth(0, 12);
            workbook.CurrentWorksheet.SetColumnWidth(1, 18);
            workbook.CurrentWorksheet.SetColumnWidth(2, 12);
            workbook.CurrentWorksheet.SetColumnWidth(3, 65);
            workbook.CurrentWorksheet.SetColumnWidth(4, 18);
            workbook.CurrentWorksheet.SetColumnWidth(5, 12);
            workbook.CurrentWorksheet.SetColumnWidth(6, 12);
            workbook.CurrentWorksheet.SetColumnWidth(7, 22);
            workbook.CurrentWorksheet.SetColumnWidth(8, 12);
            workbook.CurrentWorksheet.SetColumnWidth(9, 30);
            workbook.CurrentWorksheet.SetColumnWidth(10, 12);
            workbook.CurrentWorksheet.SetColumnWidth(11, 12);
            workbook.CurrentWorksheet.SetColumnWidth(12, 12);

            workbook.CurrentWorksheet.AddCell("OS", 0, 0);
            workbook.CurrentWorksheet.AddCell("Nome Estrutura", 1, 0);
            workbook.CurrentWorksheet.AddCell("Serviço", 2, 0);
            workbook.CurrentWorksheet.AddCell("Cartão", 3, 0);
            workbook.CurrentWorksheet.AddCell("Tipo de Desenho", 4, 0);
            workbook.CurrentWorksheet.AddCell("Tensão", 5, 0);
            workbook.CurrentWorksheet.AddCell("Circuito", 6, 0);
            workbook.CurrentWorksheet.AddCell("Cabeça", 7, 0);
            workbook.CurrentWorksheet.AddCell("Suporte", 8, 0);
            workbook.CurrentWorksheet.AddCell("Membro", 9, 0);
            workbook.CurrentWorksheet.AddCell("Inicio", 10, 0);
            workbook.CurrentWorksheet.AddCell("Finalização", 11, 0);
            workbook.CurrentWorksheet.AddCell("Total (h:m:s)", 12, 0);
            var boardHeadStyle = new Style();
            boardHeadStyle.CurrentFont.Bold = true;
            boardHeadStyle.CurrentFont.Size = 11;
            boardHeadStyle.CurrentCellXf.HorizontalAlign = CellXf.HorizontalAlignValue.center;
            boardHeadStyle.CurrentCellXf.HorizontalAlign = CellXf.HorizontalAlignValue.center;
            boardHeadStyle.Append(BasicStyles.ColorizedBackground("A6A6A6"));
            boardHeadStyle.Append(BasicStyles.BorderFrame);
            workbook.CurrentWorksheet.SetStyle("A1:M1", boardHeadStyle);

            int currentCellNumber = 1;
            foreach (var board in boardsList)
            {
                var cardFilter = Builders<TrelloCard>.Filter.Eq("BoardId", board.Id);
                var cardsQuery = await DatabaseUtils.CardsCollection(db).FindAsync(cardFilter);
                var cards = await cardsQuery.ToListAsync();

                string boardOs = string.Empty;
                Parallel.ForEach(cards, (c, token) =>
                {
                    if (c.Name.Contains("[OS] -")) { boardOs = c.Name.Replace("[OS] -", "").Trim(); }
                    else if (c.Name.Contains("OS "))
                    {
                        var nameSplit = c.Name.Split(" ");
                        try
                        {
                            int osNumber = int.Parse(nameSplit[1]);
                            boardOs = $"{osNumber}";
                        }
                        catch (Exception) { }
                    }
                });

                cards = cards.FindAll(c =>
                {
                    return c.Active &&
                        !c.CurrentList.Name.ToLower().Contains("dúvidas") &&
                        !c.CurrentList.Name.ToLower().Contains("duvidas") &&
                        !c.CurrentList.Name.ToLower().Contains("geral");
                });

                // Update the name of the member in the timers with the Board database entry
                Dictionary<string, TrelloMember> MembersCache = new();
                cards.ForEach(card =>
                {
                    card.Timers.ForEach(timer =>
                    {
                        ReplaceMemberFromBoard(board, MembersCache, timer.StartMember);
                        if (timer.EndMember is not null)
                        {
                            ReplaceMemberFromBoard(board, MembersCache, timer.EndMember);
                        }
                    });
                    ReplaceMemberFromBoard(board, MembersCache, card.CurrentMember);
                });
                SortCards(cards);

                string boardName = board.Name.Replace("TORRE", "").Trim();
                if (boardName.Contains("OS "))
                {
                    var nameSplit = boardName.Split(" ");
                    try
                    {
                        int osNumber = int.Parse(nameSplit[1]);
                        boardOs = $"{osNumber}";
                        boardName = boardName.Replace($"OS {boardOs} -", "").Trim();
                    }
                    catch (Exception) { }
                }
                foreach (var card in cards)
                {
                    var firstCellNumber = currentCellNumber;

                    var cardTagPattern = CardUtils.MatchTagPattern(card.Name);
                    if (cardTagPattern.Type != CardTagType.FULL_SAVE)
                    {
                        continue;
                    }

                    var (cardService, cardName) = CardUtils.GetCardService_Name(card.Name, cardTagPattern);

                    if (cardService == "OS")
                    {
                        continue;
                    }

                    workbook.CurrentWorksheet.AddCell(boardName, 1, currentCellNumber);
                    workbook.CurrentWorksheet.AddCell(cardService, 2, currentCellNumber);
                    workbook.CurrentWorksheet.AddCell(cardName.Trim(), 3, currentCellNumber);

                    if (boardOs != string.Empty)
                    {
                        workbook.CurrentWorksheet.AddCell(boardOs, 0, currentCellNumber);
                    }

                    var cardMembers = new Dictionary<string, TrelloMember>();
                    foreach (var timer in card.Timers)
                    {
                        cardMembers.TryAdd(timer.StartMember.Id, timer.StartMember);
                    }
                    var memberIndex = 0;

                    foreach (var member in cardMembers)
                    {
                        if (boardOs != string.Empty)
                        {
                            workbook.CurrentWorksheet.AddCell(boardOs, 0, currentCellNumber);
                        }

                        workbook.CurrentWorksheet.AddCell(boardName, 1, currentCellNumber);
                        workbook.CurrentWorksheet.AddCell(cardService, 2, currentCellNumber);
                        workbook.CurrentWorksheet.AddCell(cardName.Trim(), 3, currentCellNumber);
                        if (board.Identifiers is not null)
                        {
                            workbook.CurrentWorksheet.AddCell(board.Identifiers.Desenho, 4, currentCellNumber);
                            workbook.CurrentWorksheet.AddCell(board.Identifiers.Tensao, 5, currentCellNumber);
                            workbook.CurrentWorksheet.AddCell(board.Identifiers.Circuito, 6, currentCellNumber);
                            workbook.CurrentWorksheet.AddCell(board.Identifiers.Cabeca, 7, currentCellNumber);
                            workbook.CurrentWorksheet.AddCell(board.Identifiers.Suporte, 8, currentCellNumber);
                        }
                        var mTimers = card.Timers.FindAll((timer) => timer.StartMember.Id == member.Value.Id);

                        if (mTimers.Count > 0)
                        {
#if DEBUG
                            var firstTimer = TimeZoneInfo.ConvertTimeFromUtc(mTimers.FirstOrDefault().Start, TimeZoneInfo.Local);
                            var lastTimer = TimeZoneInfo.ConvertTimeFromUtc(mTimers.LastOrDefault().End, TimeZoneInfo.Local);
#else
                            var firstTimer = TimeZoneInfo.ConvertTimeFromUtc(mTimers.FirstOrDefault().Start, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));
                            var lastTimer = TimeZoneInfo.ConvertTimeFromUtc(mTimers.LastOrDefault().End, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));
#endif

                            workbook.CurrentWorksheet.AddCell(member.Value.Name, 9, currentCellNumber);
                            mTimers.Sort((prev, next) => prev.Start.CompareTo(next.Start));
                            workbook.CurrentWorksheet.AddCell(GetBrTimeStr(firstTimer), 10, currentCellNumber);
                            if (mTimers.LastOrDefault().State == TimeState.STOPPED)
                            {
                                workbook.CurrentWorksheet.AddCell(GetBrTimeStr(lastTimer), 11, currentCellNumber);
                            }
                            else
                            {
                                workbook.CurrentWorksheet.AddCell("", 11, currentCellNumber);
                            }
                            TimeSpan total = SumTimers(mTimers);
                            workbook.CurrentWorksheet.AddCell(DateUtils.HoursDuration(total), 12, currentCellNumber);

                            if (memberIndex < cardMembers.Count - 1)
                            {
                                currentCellNumber += 1;
                            }
                            memberIndex += 1;
                        }
                    }
                    currentCellNumber += 1;
                }
            }

            var itemsStyle = new Style();
            itemsStyle.CurrentCellXf.HorizontalAlign = CellXf.HorizontalAlignValue.center;
            itemsStyle.CurrentCellXf.VerticalAlign = CellXf.VerticalAlignValue.center;
            itemsStyle.CurrentBorder.BottomStyle = Border.StyleValue.thin;
            itemsStyle.CurrentBorder.TopStyle = Border.StyleValue.thin;
            itemsStyle.CurrentBorder.RightStyle = Border.StyleValue.thin;
            itemsStyle.CurrentBorder.LeftStyle = Border.StyleValue.thin;
            itemsStyle.CurrentBorder.BottomColor = "000000";
            itemsStyle.CurrentBorder.TopColor = "000000";
            itemsStyle.CurrentBorder.RightColor = "000000";
            itemsStyle.CurrentBorder.LeftColor = "000000";
            workbook.CurrentWorksheet.SetStyle($"A2:M{currentCellNumber}", itemsStyle);
            workbook.Save();
            GC.Collect();
        }

        public static byte[] GetExcelLocal(string boardId, IMongoClient mDbClient, out string fileName)
        {
            var boardsCollection = DatabaseUtils.BoardsCollection(mDbClient);
            var boardFilter = Builders<TrelloBoard>.Filter.Eq("Id", boardId);
            var board = boardsCollection.Find(boardFilter).FirstOrDefault();

            var cardsCollection = DatabaseUtils.CardsCollection(mDbClient);
            var cardFilter = Builders<TrelloCard>.Filter.Eq("BoardId", boardId);
            var cards = cardsCollection.Find(cardFilter).ToList();
            cards = cards.FindAll(c =>
                c.Active && !c.CurrentList.Name.ToLower().Contains("dúvidas") && !c.CurrentList.Name.ToLower().Contains("duvidas") && !c.CurrentList.Name.ToLower().Contains("geral"));

            // Update the name of the member in the timers with the Board database entry
            Dictionary<string, TrelloMember> MembersCache = new();
            cards.ForEach(card =>
            {
                SortCardTimers(card);
                card.Timers.ForEach(timer =>
                {
                    ReplaceMemberFromBoard(board, MembersCache, timer.StartMember);
                    if (timer.EndMember is not null)
                    {
                        ReplaceMemberFromBoard(board, MembersCache, timer.EndMember);
                    }
                });
            });

            string wbName = $"Resumo - {board.Name}";

            //fileName = $"{wbName}-{DateTime.UtcNow.Day}-{DateTime.UtcNow.Month}-{DateTime.UtcNow.Year}-{DateTime.UtcNow.Hour}-{DateTime.UtcNow.Minute}.xlsx";
            fileName = $"{wbName}.xlsx";
            {
                Workbook workbook = new(fileName, wbName);
                FirstPage(cards, wbName, workbook);
                workbook.AddWorksheet("Detalhes");
                workbook.SetCurrentWorksheet("Detalhes");
                SecondPage(cards, board.Members, wbName, workbook, board);

                workbook.Save();
            }
            var f = System.IO.File.Open(fileName, FileMode.Open);
            byte[] bytes = new byte[f.Length];
            f.Read(bytes, 0, Convert.ToInt32(f.Length));
            f.Close();
            //System.IO.File.Delete(fileName);
            return bytes;
        }

        public static void SortCardTimers(TrelloCard card)
        {
            card.Timers.Sort((t1, t2) =>
            {
                return t1.Start.CompareTo(t2.Start);
            });
        }

        private static void SecondPage(List<TrelloCard> cards, List<TrelloMember> members, string wbName, Workbook workbook, TrelloBoard board)
        {
            workbook.CurrentWorksheet.SetColumnWidth(0, 12);
            workbook.CurrentWorksheet.SetColumnWidth(1, 65);
            workbook.CurrentWorksheet.SetColumnWidth(2, 30);

            workbook.CurrentWorksheet.SetColumnWidth(3, 18);
            workbook.CurrentWorksheet.SetColumnWidth(4, 12);
            workbook.CurrentWorksheet.SetColumnWidth(5, 12);
            workbook.CurrentWorksheet.SetColumnWidth(6, 22);
            workbook.CurrentWorksheet.SetColumnWidth(7, 12);

            workbook.CurrentWorksheet.SetColumnWidth(8, 12);
            workbook.CurrentWorksheet.SetColumnWidth(9, 12);
            workbook.CurrentWorksheet.SetColumnWidth(10, 12);

            WorksheetHeader(wbName, workbook, 11);

            var boardHeadStyle = new Style();
            boardHeadStyle.CurrentFont.Bold = true;
            boardHeadStyle.CurrentFont.Size = 11;
            boardHeadStyle.CurrentCellXf.HorizontalAlign = CellXf.HorizontalAlignValue.center;
            boardHeadStyle.CurrentCellXf.HorizontalAlign = CellXf.HorizontalAlignValue.center;
            boardHeadStyle.Append(BasicStyles.ColorizedBackground("A6A6A6"));
            boardHeadStyle.Append(BasicStyles.BorderFrame);

            workbook.CurrentWorksheet.MergeCells("A4:E4");
            workbook.CurrentWorksheet.AddCell("Serviço", "A5");
            workbook.CurrentWorksheet.AddCell("Cartão", "B5");

            workbook.CurrentWorksheet.AddCell("Tipo de Desenho", "C5");
            workbook.CurrentWorksheet.AddCell("Tensão", "D5");
            workbook.CurrentWorksheet.AddCell("Circuito", "E5");
            workbook.CurrentWorksheet.AddCell("Cabeça", "F5");
            workbook.CurrentWorksheet.AddCell("Suporte", "G5");

            workbook.CurrentWorksheet.AddCell("Membro", "H5");
            workbook.CurrentWorksheet.AddCell("Inicio", "I5");
            workbook.CurrentWorksheet.AddCell("Finalização", "J5");
            workbook.CurrentWorksheet.AddCell("Total (h:m)", "K5");

            workbook.CurrentWorksheet.SetStyle("A4:K5", boardHeadStyle);
            int currentCellNumber = 6;
            SortCards(cards);

            foreach (var card in cards)
            {
                var firstCellNumber = currentCellNumber;

                var cardServicePattern = CardUtils.MatchTagPattern(card.Name);
                if (cardServicePattern.Type != CardTagType.FULL_SAVE) continue;

                var (cardService, cardName) = CardUtils.GetCardService_Name(card.Name, cardServicePattern);
                //GetCardServiceAndTag(card, out string cardName, out string service);

                workbook.CurrentWorksheet.AddCell(cardService, $"A{currentCellNumber}");
                workbook.CurrentWorksheet.AddCell(cardName.Trim(), $"B{currentCellNumber}");

                if(board.Identifiers is not null){
                    var identifiers = board.Identifiers!;
                    workbook.CurrentWorksheet.AddCell(identifiers.Desenho, $"C{currentCellNumber}");
                    workbook.CurrentWorksheet.AddCell(identifiers.Tensao, $"D{currentCellNumber}");
                    workbook.CurrentWorksheet.AddCell(identifiers.Circuito, $"E{currentCellNumber}");
                    workbook.CurrentWorksheet.AddCell(identifiers.Cabeca, $"F{currentCellNumber}");
                    workbook.CurrentWorksheet.AddCell(identifiers.Suporte, $"G{currentCellNumber}");
                }

                var cardMembers = new Dictionary<string, TrelloMember>();
                foreach (var timer in card.Timers)
                {
                    cardMembers.TryAdd(timer.StartMember.Id, timer.StartMember);
                }
                var memberIndex = 0;

                foreach (var member in cardMembers)
                {
                    var mTimers = card.Timers.FindAll((timer) => timer.StartMember.Id == member.Value.Id);

                    if (mTimers.Count > 0)
                    {
#if DEBUG
                        var firstTimer = TimeZoneInfo.ConvertTimeFromUtc(mTimers.FirstOrDefault().Start, TimeZoneInfo.Local);
                        var lastTimer = TimeZoneInfo.ConvertTimeFromUtc(mTimers.LastOrDefault().End, TimeZoneInfo.Local);
#else
                        var firstTimer = TimeZoneInfo.ConvertTimeFromUtc(mTimers.FirstOrDefault().Start, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));
                        var lastTimer = TimeZoneInfo.ConvertTimeFromUtc(mTimers.LastOrDefault().End, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));
#endif

                        workbook.CurrentWorksheet.AddCell(member.Value.Name, $"H{currentCellNumber}");
                        workbook.CurrentWorksheet.AddCell(GetBrTimeStr(firstTimer), $"I{currentCellNumber}");
                        if (mTimers.LastOrDefault().State == TimeState.STOPPED)
                        {
                            workbook.CurrentWorksheet.AddCell(GetBrTimeStr(lastTimer), $"J{currentCellNumber}");
                        }
                        else
                        {
                            workbook.CurrentWorksheet.AddCell("", $"J{currentCellNumber}");
                        }
                        TimeSpan total = SumTimers(mTimers);
                        workbook.CurrentWorksheet.AddCell(DateUtils.HoursDuration(total), $"K{currentCellNumber}");

                        if (memberIndex < cardMembers.Count - 1)
                        {
                            currentCellNumber += 1;
                        }
                        memberIndex += 1;
                    }
                }

                workbook.CurrentWorksheet.MergeCells($"A{firstCellNumber}:A{currentCellNumber}");
                workbook.CurrentWorksheet.MergeCells($"B{firstCellNumber}:B{currentCellNumber}");
                currentCellNumber += 1;
            }
            var itemsStyle = new Style();
            itemsStyle.CurrentCellXf.HorizontalAlign = CellXf.HorizontalAlignValue.center;
            itemsStyle.CurrentCellXf.VerticalAlign = CellXf.VerticalAlignValue.center;
            itemsStyle.CurrentBorder.BottomStyle = Border.StyleValue.thin;
            itemsStyle.CurrentBorder.TopStyle = Border.StyleValue.thin;
            itemsStyle.CurrentBorder.RightStyle = Border.StyleValue.thin;
            itemsStyle.CurrentBorder.LeftStyle = Border.StyleValue.thin;
            itemsStyle.CurrentBorder.BottomColor = "000000";
            itemsStyle.CurrentBorder.TopColor = "000000";
            itemsStyle.CurrentBorder.RightColor = "000000";
            itemsStyle.CurrentBorder.LeftColor = "000000";
            workbook.CurrentWorksheet.SetStyle($"A6:K{currentCellNumber}", itemsStyle);

        }
        private static void FirstPage(List<TrelloCard> cards, string wbName, Workbook workbook)
        {
            workbook.CurrentWorksheet.SetColumnWidth(0, 12);
            workbook.CurrentWorksheet.SetColumnWidth(1, 65);
            workbook.CurrentWorksheet.SetColumnWidth(2, 12);
            workbook.CurrentWorksheet.SetColumnWidth(3, 12);
            workbook.CurrentWorksheet.SetColumnWidth(4, 12);

            // CABEÇALHO
            WorksheetHeader(wbName, workbook, 5);
            /// FIM CABEÇALHO

            var boardHeadStyle = new Style();
            boardHeadStyle.CurrentFont.Bold = true;
            boardHeadStyle.CurrentFont.Size = 11;
            boardHeadStyle.CurrentCellXf.HorizontalAlign = CellXf.HorizontalAlignValue.center;
            boardHeadStyle.Append(BasicStyles.ColorizedBackground("A6A6A6"));
            boardHeadStyle.Append(BasicStyles.BorderFrame);

            workbook.CurrentWorksheet.MergeCells("A4:E4");
            workbook.CurrentWorksheet.AddCell("Serviço", "A5");
            workbook.CurrentWorksheet.AddCell("Cartão", "B5");
            workbook.CurrentWorksheet.AddCell("Inicio", "C5");
            workbook.CurrentWorksheet.AddCell("Finalização", "D5");
            workbook.CurrentWorksheet.AddCell("Total (h:m)", "E5");

            workbook.CurrentWorksheet.SetStyle("A4:E5", boardHeadStyle);

            workbook.CurrentWorksheet.SetCurrentCellAddress("A6");
            int currentCellNumber = 6;
            SortCards(cards);
            var prevService = "";
            var count = 0;
            List<string> spacerCells = new();
            foreach (var card in cards)
            {
                var cardTagPattern = CardUtils.MatchTagPattern(card.Name);
                if (cardTagPattern.Type != CardTagType.FULL_SAVE) continue;

                var cardRow = GetCardRow(card, cardTagPattern);

                if (cardRow["servico"].ToString() != prevService && count > 0)
                {
                    var l = new List<string>() { "", "", " ", " ", " " };
                    workbook.CurrentWorksheet.AddCellRange(l, $"A{currentCellNumber}:E{currentCellNumber}");
                    spacerCells.Add($"A{currentCellNumber}:E{currentCellNumber}");
                    currentCellNumber += 1;
                }
                prevService = cardRow["servico"].ToString();
                count += 1;
                workbook.CurrentWorksheet.AddCellRange(cardRow.Values.ToList(), $"A{currentCellNumber}:E{currentCellNumber}");
                currentCellNumber += 1;
            }

            var borderStyle = new Style();
            borderStyle.CurrentBorder.TopStyle = Border.StyleValue.thin;
            borderStyle.CurrentBorder.BottomStyle = Border.StyleValue.thin;
            borderStyle.CurrentBorder.RightStyle = Border.StyleValue.thin;
            borderStyle.CurrentBorder.LeftStyle = Border.StyleValue.thin;
            borderStyle.CurrentBorder.BottomColor = "000000";
            borderStyle.CurrentBorder.TopColor = "000000";
            borderStyle.CurrentBorder.RightColor = "000000";
            borderStyle.CurrentBorder.LeftColor = "000000";
            borderStyle.CurrentCellXf.HorizontalAlign = CellXf.HorizontalAlignValue.center;
            workbook.CurrentWorksheet.SetStyle($"A6:E{currentCellNumber - 1}", borderStyle);

            var cardNameStyle = new Style();
            cardNameStyle.CurrentCellXf.HorizontalAlign = CellXf.HorizontalAlignValue.left;
            cardNameStyle.Append(BasicStyles.BorderFrame);
            workbook.CurrentWorksheet.SetStyle($"B6:B{currentCellNumber - 1}", cardNameStyle);

            var spacerCellStyle = new Style();
            spacerCellStyle.Append(borderStyle);
            spacerCellStyle.Append(BasicStyles.ColorizedBackground("A6A6A6"));

            foreach (string range in spacerCells)
            {
                workbook.CurrentWorksheet.SetStyle(range, spacerCellStyle);
                workbook.CurrentWorksheet.MergeCells(range);
            }
        }

        private static void SortCards(List<TrelloCard> cards)
        {
            List<KeyValuePair<string, int>> prioridades = new()
            {
                new KeyValuePair<string, int>("[estudo]", 1),
                new KeyValuePair<string, int>("[modelagem]", 2),
                new KeyValuePair<string, int>("[desenho]", 3),
                new KeyValuePair<string, int>("[conferência]", 4),
                new KeyValuePair<string, int>("[conferencia]", 4),
            };

            foreach (var card in cards)
            {
                SortCardTimers(card);
            }

            cards.Sort((p, n) =>
            {
                var x = p.Name.ToLowerInvariant();
                var y = n.Name.ToLowerInvariant();

                var anyX = prioridades.Any(z => x.Contains(z.Key));
                var anyY = prioridades.Any(z => y.Contains(z.Key));

                if (anyX || anyY)
                {
                    var firstX = prioridades.FirstOrDefault(z => x.Contains(z.Key));
                    var firstY = prioridades.FirstOrDefault(z => y.Contains(z.Key));
                    if (anyX && anyY)
                    {
                        if (firstX.Value > firstY.Value)
                        {
                            return firstX.Value;
                        }
                        else if (firstX.Value == firstY.Value)
                        {
                            return x.CompareTo(y);
                        }

                        return -firstX.Value;
                    }

                    if (anyX) return -firstX.Value;
                    if (anyY) return firstY.Value;
                }

                return x.CompareTo(y);
            });
        }

        private static void WorksheetHeader(string wbName, Workbook workbook, int span)
        {
            char[] letters = { '0', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'N' };
            var boldText = new Style();
            boldText.CurrentFont.Bold = true;
            boldText.CurrentFont.Size = 11;
            var centredText = new Style();
            centredText.CurrentCellXf.HorizontalAlign = CellXf.HorizontalAlignValue.center;
            centredText.CurrentFont.Bold = true;
            centredText.CurrentFont.Size = 14;

            centredText.Append(BasicStyles.ColorizedBackground("A6A6A6"));
            centredText.Append(BasicStyles.BorderFrame);
            boldText.Append(BasicStyles.BorderFrame);

            workbook.CurrentWorksheet.AddCell(wbName, "A1", centredText);

            workbook.CurrentWorksheet.AddCell("Serviço: ", "A2");
            // workbook.CurrentWorksheet.SetStyle("B2:D2", tableStyle);

            workbook.CurrentWorksheet.AddCell("Estrutura: ", "A3");
            // workbook.CurrentWorksheet.SetStyle("B3:D3", tableStyle);

            workbook.CurrentWorksheet.AddCell("OS: ", $"{letters[span]}2");

            workbook.CurrentWorksheet.MergeCells($"A1:{letters[span]}1");
            workbook.CurrentWorksheet.MergeCells($"A2:{letters[span - 1]}2");
            workbook.CurrentWorksheet.MergeCells($"A3:{letters[span - 1]}3");
            workbook.CurrentWorksheet.SetStyle("A2", boldText);
            workbook.CurrentWorksheet.SetStyle("A3", boldText);
            workbook.CurrentWorksheet.SetStyle($"{letters[span]}2", boldText);
            workbook.CurrentWorksheet.SetStyle("B2", BasicStyles.BorderFrame);
            workbook.CurrentWorksheet.SetStyle("C2", BasicStyles.BorderFrame);
            workbook.CurrentWorksheet.SetStyle("D2", BasicStyles.BorderFrame);
            workbook.CurrentWorksheet.SetStyle("B3", BasicStyles.BorderFrame);
            workbook.CurrentWorksheet.SetStyle("C3", BasicStyles.BorderFrame);
            workbook.CurrentWorksheet.SetStyle("D3", BasicStyles.BorderFrame);
            workbook.CurrentWorksheet.SetStyle("E3", BasicStyles.BorderFrame);

            if (span == 6)
            {
                workbook.CurrentWorksheet.SetStyle("E2", BasicStyles.BorderFrame);
                workbook.CurrentWorksheet.SetStyle("F3", BasicStyles.BorderFrame);
            }
        }

        private static Dictionary<string, object> GetCardRow(TrelloCard card, CardTagPattern tagPattern)
        {

            var list = new Dictionary<string, object>();
            var (service, cardName) = CardUtils.GetCardService_Name(card.Name, tagPattern);

            list.Add("servico", service);
            list.Add("cartao", cardName.Trim());
            var firstTimer = card.Timers.FirstOrDefault();
            if (firstTimer == null)
            {
                list.Add("inicio", "");
            }
            else
            {
                var firstTimerDate = DateUtils.ToBrSpTimezone(firstTimer.Start);
                list.Add("inicio", GetBrTimeStr(firstTimerDate));
            }
            var lastTimer = card.Timers.LastOrDefault();
            if (lastTimer == null)
            {
                list.Add("finalizacao", "");
            }
            else
            {
                if (lastTimer.State == TimeState.STOPPED)
                {
                    var lastTimerDate = DateUtils.ToBrSpTimezone(lastTimer.End);
                    list.Add("finalizacao", GetBrTimeStr(lastTimerDate));
                }
                else
                {
                    list.Add("finalizacao", "");
                }

            }
            TimeSpan totalTime = SumTimers(card.Timers);
            list.Add("tempoTotal", DateUtils.HoursDuration(totalTime));
            return list;
        }

        private static TimeSpan SumTimers(List<CardTime> timers)
        {
            TimeSpan totalTime = new();
            foreach (var timer in timers)
            {
                var end = (timer.State == TimeState.RUNNING) ? DateTime.UtcNow : timer.End;
                totalTime += (end - timer.Start);
            }

            return totalTime;
        }

        private static void GetCardServiceAndTag(TrelloCard card, out string cardName, out string service)
        {
            cardName = card.Name;
            var initPos = cardName.IndexOf('[');
            var endPos = cardName.IndexOf(']');
            service = "";
            if (initPos != -1 && endPos != -1)
            {
                service = card.Name.Substring(initPos + 1, endPos - 1).Trim();

                cardName = cardName.Replace($"[{service}] - ", "").Trim();
                cardName = cardName.Replace($"[{service}]-", "").Trim();
                cardName = cardName.Replace($"[{service}]", "").Trim();

                service = service[0].ToString().ToUpper() + service.Remove(0, 1).ToLower();
                service = service.Trim();
            }
        }

        private static string GetBrTimeStr(DateTime time)
        {
            var day = (time.Day < 10) ? $"0{time.Day}" : time.Day.ToString();
            var month = (time.Month < 10) ? $"0{time.Month}" : time.Month.ToString();

            return $"{day}/{month}/{time.Year}";
        }
    }
}
