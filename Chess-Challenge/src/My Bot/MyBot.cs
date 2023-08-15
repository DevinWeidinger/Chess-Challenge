using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    private readonly List<Move> _myMoves = new();
    
    public Move Think(Board board, Timer timer)
    {
        var move = MinimaxSearch(board, depth: 2);
        _myMoves.Add(move);
        return move;
    }

    private Move MinimaxSearch(Board board, int depth)
    {
        var moves = board.GetLegalMoves();
        
        if (TryCheckMate(moves, board, out var checkMateMove))
        {
            Console.WriteLine("Found a checkmate!");
            return checkMateMove;
        }

        if (TryHighestValueCapture(moves, board, safeOnly: true, out var highestValueSafeCapture))
        {
            Console.WriteLine($"Found a safe capture! {highestValueSafeCapture.MovePieceType} captures {highestValueSafeCapture.CapturePieceType}");
            return highestValueSafeCapture;
        }

        if (TryHighestValueCapture(moves, board, safeOnly: false, out var highestValueCapture))
        {
            Console.WriteLine($"Found a capture! {highestValueCapture.MovePieceType} captures {highestValueCapture.CapturePieceType}");
            return highestValueCapture;
        }

        if (TrySafeCheck(moves, board, out var safeCheck))
        {
            Console.WriteLine($"Found a safe check! Target Index:{safeCheck.TargetSquare.Index} Enemy King Index:{board.GetKingSquare(!board.IsWhiteToMove).Index}");
            return safeCheck;
        }
        
        if(_myMoves.Count > 0 && TrySafeEscapeAfterCapture(_myMoves.Last(), moves, board, out var safeEscape))
        {
            Console.WriteLine($"Found a safe escape after capture! Saved: {safeEscape.MovePieceType}");
            return safeEscape;
        }

        if (TryCastle(moves, out var castleMove))
        {
            Console.WriteLine("Castling!");
            return castleMove;
        }

        Move bestMove = default;
        var bestValue = int.MinValue;
        foreach (var move in moves)
        {
            if(_myMoves.Count % 3 == 0 && move.MoveStrengthensPawnStructure(board))
                return move;
            
            board.MakeMove(move);
            var moveValue = -AlphaBeta(board, depth - 1, int.MinValue, int.MaxValue);
            board.UndoMove(move);
            if (moveValue <= bestValue) continue;
            bestValue = moveValue;
            bestMove = move;
        }

        return bestMove;
    }

    private static bool TryCheckMate(IEnumerable<Move> moves, Board board, out Move output)
    {
        output = moves.FirstOrDefault(m => m.IsCheckmate(board));
        return output != default;
    }
    
    private static bool TryHighestValueCapture(IEnumerable<Move> moves, Board board, bool safeOnly, out Move output)
    {
        output = default;
        var highestValue = int.MinValue;
        foreach (var move in moves.Where(move => move.IsCapture))
        {
            if (safeOnly && board.SquareIsAttackedByOpponent(move.TargetSquare)) continue;
            var captureValue = move.CapturePieceType.GetValue();
            var pieceValue = move.MovePieceType.GetValue();
            if (safeOnly == false && pieceValue > captureValue) continue; //skip unfavorable trades
            if (captureValue - pieceValue <= highestValue) continue; //filter out lower value captures
            highestValue = captureValue - pieceValue;
            output = move;
        }

        return output != default;
    }

    private static bool TrySafeCheck(IEnumerable<Move> moves, Board board, out Move output)
    {
        output = moves.FirstOrDefault(m => m.IsCheck(board) && !board.SquareIsAttackedByOpponent(m.TargetSquare));
        return output != default;
    }

    private static bool TrySafeEscapeAfterCapture(Move lastMove, IEnumerable<Move> moves, Board board, out Move output)
    {
        output = default;
        var validRequest = lastMove != default && lastMove.IsCapture && lastMove.TargetSquare.ContainsMyPiece(board) && board.SquareIsAttackedByOpponent(lastMove.TargetSquare);
        if(validRequest == false) return false;
        foreach (var move in moves.Where(m => m.StartSquare == lastMove.TargetSquare))
        {
            if (board.SquareIsAttackedByOpponent(move.TargetSquare)) continue;
            Console.WriteLine("Found Safe Exit After Capture!");
            output =  move;
            break;
        }
        return output != default;
    }

    private static bool TryCastle(IEnumerable<Move> moves, out Move output)
    {
        output = moves.FirstOrDefault(m => m.IsCastles);
        return output != default;
    }
    
    private static int AlphaBeta(Board board, int depth, int alpha, int beta)
    {
        if (depth == 0)
            return board.GetValue();

        var allMoves = board.GetLegalMoves();
        var value = int.MinValue;
        foreach (var move in allMoves)
        {
            board.MakeMove(move);
            value = Math.Max(value, AlphaBeta(board, depth - 1, alpha, beta));
            board.UndoMove(move);
            alpha = Math.Max(alpha, value);
            if (beta <= alpha) break;
        }
        return value;
    }
}

public static class Extensions
{
    public static bool IsCheckmate(this Move move, Board board)
    {
        board.MakeMove(move);
        var isMate = board.IsInCheckmate();
        board.UndoMove(move);
        return isMate;
    }
    
    public static bool IsCheck(this Move move, Board board)
    {
        board.MakeMove(move);
        var isCheck = board.IsInCheck();
        board.UndoMove(move);
        return isCheck;
    }

    public static bool MoveStrengthensPawnStructure(this Move move, Board board)
    {
        if(move.MovePieceType != PieceType.Pawn)
            return false;
        
        PieceList myPawns = board.GetPieceList(PieceType.Pawn, board.IsWhiteToMove);
        Square target = move.TargetSquare;
        foreach (var myPawn in myPawns)
        {
            ulong attacks = BitboardHelper.GetPawnAttacks(myPawn.Square, myPawn.IsWhite);
            ulong targetBit = 1UL << target.Index; //if target square is protected by pawn, return true
            if ((attacks & targetBit) != 0)
                return true;
        }
        
        return false; 
    }
    
    public static int GetValue(this PieceType pieceType)
    {
        return pieceType switch
        {
            PieceType.None => 0,
            PieceType.Pawn => 1,
            PieceType.Knight => 3,
            PieceType.Bishop => 3,
            PieceType.Rook => 5,
            PieceType.Queen => 9,
            PieceType.King => 10,
            _ => 0
        };
    }
    
    public static int GetValue(this Board board)
    {
        var evaluation = 0;
        for (var square = 0; square < 64; square++)
        {
            var piece = board.GetPiece(new Square(index:square));
            if (board.IsWhiteToMove)
            {
                if (piece.IsWhite) evaluation += piece.PieceType.GetValue();
                else evaluation -= piece.PieceType.GetValue();
            }
            else
            {
                if (piece.IsWhite) evaluation -= piece.PieceType.GetValue();
                else evaluation += piece.PieceType.GetValue();
            }
        }

        return evaluation;
    }

    public static bool ContainsMyPiece(this Square square, Board board)
    {
        var piece = board.GetPiece(square);
        if (piece.IsNull) return false;
        switch (board.IsWhiteToMove)
        {
            case true when piece.IsWhite:
            case false when piece.IsWhite == false:
                return true;
            default:
                return false;
        }
    }
}