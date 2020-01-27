﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Caliburn.Micro;
using Plugin.Media;
using Plugin.Media.Abstractions;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using Puzzle_Image_Recognition.Sudoku_Normal;
using Sudoku_Solver.Data;
using Sudoku_Solver.Initiation;
using Sudoku_Solver.Solver;
using Sudoku_Solver_Xamarin.DependencyServiceInterfaces;
using Sudoku_Solver_Xamarin.Resources;
using Xamarin.Forms;
using Cell = Sudoku_Solver.Data.Cell;

namespace Sudoku_Solver_Xamarin.ViewModels
{
    class HomeViewModel : PropertyChangedBase
    {
        private readonly SudokuImageParser parser;
        private int curPuzzle = 0;

        private BoardModel boardPrivate;
        public BoardModel Board
        {
            get { return boardPrivate; }
            set
            {
                boardPrivate = value;
                NotifyOfPropertyChange(nameof(Board));
            }
        }

        private bool isLoading;
        public bool IsLoading
        {
            get { return isLoading; }
            set
            {
                isLoading = value;
                NotifyOfPropertyChange(nameof(IsLoading));
            }
        }

        private bool showSnackbar;
        public bool ShowSnackbar
        {
            get { return showSnackbar; }
            set
            {
                showSnackbar = value;
                NotifyOfPropertyChange(nameof(ShowSnackbar));
            }
        }

        private string statusText;
        public string StatusText
        {
            get { return statusText; }
            set
            {
                statusText = value;
                NotifyOfPropertyChange(nameof(StatusText));
            }
        }

        public HomeViewModel()
        {
            SolvePuzzleCommand = new Command(execute: () =>
            {
                SolvePuzzle();
            });
            VerifyPuzzleCommand = new Command(execute: () =>
            {
                VerifyPuzzle();
            });
            ClearPuzzleCommand = new Command(execute: () =>
            {
                ClearPuzzle();
            });
            TakeImageAndParseCommand = new Command(execute: () =>
            {
                TakeImageAndParse();
            });
            NewPuzzleCommand = new Command(execute: () =>
            {
                NewPuzzle();
            });
            CellSelectedCommand = new Command(execute: (object param) =>
            {
                CellSelected(param);
            });
            DigitClickedCommand = new Command(execute: (object digit) =>
            {
                DigitSelected(digit);
            });
            IsLoading = false;
            StatusText = "";
            Board = new BoardModel();
            parser = new SudokuImageParser();
            BoardInitiation.InitBasicBoard(Board);
        }

        public ICommand SolvePuzzleCommand { get; }
        public ICommand VerifyPuzzleCommand { get; }
        public ICommand ClearPuzzleCommand { get; }
        public ICommand TakeImageAndParseCommand { get; }
        public ICommand NewPuzzleCommand { get; }
        public ICommand CellSelectedCommand { get; }
        public ICommand DigitClickedCommand { get; }

        public void SolvePuzzle()
        {
            Thread thread = new Thread(() =>
            {
                IsLoading = true;
                Board = Solver.PuzzleSolver(Board, GroupGetter.GetStandardGroups(Board));
                IsLoading = false;
                UpdateStatus(PuzzleVerifier.VerifyPuzzle(Board) ? MagicStrings.SOLVED : MagicStrings.NOT_SOLVED);
            });
            thread.Start();
        }
        private void UpdateStatus(string message)
        {
            StatusText = message;
            Thread messageThread = new Thread(() =>
            {
                ShowSnackbar = true;
                DateTime to = DateTime.Now.AddMilliseconds(3000);
                while (DateTime.Now < to) { }
                ShowSnackbar = false;
            });
            messageThread.Start();
        }

        public void VerifyPuzzle()
        {
            StatusText = MagicStrings.VERIFYING;
            IsLoading = true;
            bool verify = PuzzleVerifier.VerifyPuzzle(Board);
            IsLoading = false;
            UpdateStatus(verify ? MagicStrings.VALID_SOLUTION : MagicStrings.INVALID_SOLUTION);
        }

        public void DigitSelected(object digit)
        {
            string d = (string)digit;
            foreach(var row in Board.BoardValues)
            {
                foreach(Cell cell in row)
                {
                    if(cell.Selected)
                    {
                        cell.CellValue = d;
                        break;
                    }
                }
            }
        }

        public void ClearPuzzle()
        {
            BoardInitiation.ClearBoard(Board);
        }

        public void CellSelected(object cell)
        {
            Cell c = (Cell)cell;
            foreach (var row in Board.BoardValues)
            {
                foreach(Cell cell1 in row)
                {
                    if (!cell1.Equals(c)) cell1.Selected = false;
                }
            }
        }

        public void NewPuzzle()
        {
            ClearPuzzle();
            string puzzle = TestInputs.UNSOLVED_BOARD_EASY;
            switch (curPuzzle)
            {
                case 0:
                    puzzle = TestInputs.UNSOLVED_BOARD_EASY;
                    break;
                case 1:
                    puzzle = TestInputs.UNSOLVED_BOARD_MEDIUM;
                    break;
                case 2:
                    puzzle = TestInputs.UNSOLVED_BOARD_HARD;
                    break;
                case 3:
                    puzzle = TestInputs.UNSOLVED_BOARD_EXTREME;
                    curPuzzle = -1;
                    break;
            }
            curPuzzle++;
            BoardInitiation.InitCommaSeperatedBoard(Board, puzzle);
        }

        public async void TakeImageAndParse()
        {
            //TODO Should replace this with a messaging service to maintain seperation of viewmodel and view
            bool existing = await Application.Current.MainPage.DisplayAlert("Parse Sudoku", "Would you like to take a photo or upload an existing one?", "Upload", "Take");
            Stream photo;
            if(existing)
            {
                photo = await DependencyService.Get<IPhotoPickerService>().GetImageStreamAsync();
            }
            else
            {
                photo = await TakePhoto();
            }            
            if (photo != null)
            {
                using (var memoryStream = new MemoryStream())
                {
                    photo.CopyTo(memoryStream);
                    byte[] photoBytes = memoryStream.ToArray();
                    ParsePuzzle(photoBytes);
                }
            }
        }
        private async Task<Stream> TakePhoto()
        {
            PermissionStatus status = await CrossPermissions.Current.CheckPermissionStatusAsync(Permission.Camera);
            if (status != PermissionStatus.Granted)
            {
                var granted = await CrossPermissions.Current.RequestPermissionsAsync(new Permission[] { Permission.Camera });
                status = granted[Permission.Camera];
            }
            if (status == PermissionStatus.Granted)
            {
                var photo = await CrossMedia.Current.TakePhotoAsync(new StoreCameraMediaOptions() { });
                return photo.GetStream();
            }
            return null;
        }
    

        private void ParsePuzzle(byte[] file)
        {
            int[,] board = parser.Solve(file);

            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    int val = board[i, j];
                    if (val != 0)
                    {
                        Board.BoardValues[i][j].CellValue = val.ToString();
                    }
                    else
                    {
                        Board.BoardValues[i][j].CellValue = string.Empty;
                    }
                }
            }
        }
    }
}
