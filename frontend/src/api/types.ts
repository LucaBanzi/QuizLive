// Tipi speculari ai DTO del backend (backend/src/QuizLive.Api/Dtos/Dtos.cs)
export type GamePhase =
  | 'Lobby' | 'Ready' | 'QuestionShown' | 'AnswersOpen'
  | 'AnswersClosed' | 'AnswerRevealed' | 'Leaderboard' | 'Finished';
export type MediaType = 'None' | 'Image' | 'Video' | 'Audio';
export type Label = 'A' | 'B' | 'C' | 'D';

export interface OptionDto { label: Label; text: string }
export interface QuestionDto {
  id: string; index: number; total: number; text: string;
  mediaType: MediaType; mediaUrl: string | null; options: OptionDto[];
}
export interface RevealDto { correctLabel: Label; explanation: string | null; votesByLabel: Record<string, number> }
export interface LeaderboardEntryDto {
  rank: number; teamId: string; nickname: string; tableNumber: number;
  score: number; correctCount: number; lastQuestionPoints: number;
}
export interface GameStateDto {
  seq: number; phase: GamePhase; question: QuestionDto | null; reveal: RevealDto | null;
  answeredCount: number; activeTeams: number; leaderboard: LeaderboardEntryDto[] | null; quizName: string;
}
export interface PlayerStateDto {
  seq: number; phase: GamePhase; questionId: string | null; questionIndex: number; totalQuestions: number;
  myAnswer: Label | null; isCorrect: boolean | null; correctLabel: Label | null;
  score: number; rank: number; nickname: string;
}
export interface TeamAdminDto {
  id: string; nickname: string; spokespersonName: string; tableNumber: number; score: number;
  isActive: boolean; isConnected: boolean; answeredCurrent: boolean; selectedLabel: Label | null;
  recoveryOpenUntil: string | null;
}
export interface AdminStateDto {
  game: GameStateDto; currentCorrectLabel: Label | null; nextQuestionPreview: string | null; teams: TeamAdminDto[];
}
export interface AnswerAckDto { status: 'Accepted' | 'AlreadyAnswered' | 'Closed' | 'WrongQuestion' | 'NotRegistered'; storedLabel: Label | null }
export interface RegisterResult { teamId: string; sessionToken: string; state: PlayerStateDto }
export interface RejoinResult { teamId: string; sessionToken: string; state: PlayerStateDto }

export interface QuestionEdit {
  text: string; mediaType: MediaType; mediaUrl: string | null; correctLabel: Label;
  explanation: string | null; options: OptionDto[];
}
export interface QuestionAdminDto extends QuestionEdit { id: string; orderIndex: number }
export interface PublicInfo { quizName: string; publicOrigin: string }
