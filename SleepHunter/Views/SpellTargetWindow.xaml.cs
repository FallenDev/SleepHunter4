﻿using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Animation;
using System.Windows.Threading;

using SleepHunter.Extensions;
using SleepHunter.Metadata;
using SleepHunter.Models;
using SleepHunter.Settings;

namespace SleepHunter.Views
{
    public partial class SpellTargetWindow : Window
    {
        private double baseHeight;
        private SpellQueueItem spellQueueItem = new SpellQueueItem();

        public SpellQueueItem SpellQueueItem
        {
            get { return spellQueueItem; }
            private set { spellQueueItem = value; }
        }

        public Spell Spell
        {
            get { return (Spell)GetValue(SpellProperty); }
            set { SetValue(SpellProperty, value); }
        }

        public bool IsEditMode
        {
            get { return (bool)GetValue(IsEditModeProperty); }
            set { SetValue(IsEditModeProperty, value); }
        }

        public static readonly DependencyProperty IsEditModeProperty =
            DependencyProperty.Register("IsEditMode", typeof(bool), typeof(SpellTargetWindow), new PropertyMetadata(false));

        public static readonly DependencyProperty SpellProperty =
            DependencyProperty.Register("Spell", typeof(Spell), typeof(SpellTargetWindow), new PropertyMetadata(null));

        public SpellTargetWindow(Spell spell, SpellQueueItem item, bool isEditMode = true)
           : this(spell)
        {
            if (isEditMode)
            {
                Title = "Edit Target";
                okButton.Content = "_Save Changes";
            }

            SpellQueueItem.Id = item.Id;
            SetTargetForMode(item.Target);

            maxLevelCheckBox.IsChecked = item.HasTargetLevel;

            if (item.HasTargetLevel)
                maxLevelUpDown.Value = item.TargetLevel.Value;

            IsEditMode = isEditMode;
        }

        public SpellTargetWindow(Spell spell)
           : this()
        {
            Spell = spell;

            maxLevelUpDown.Value = spell.MaximumLevel;
            maxLevelCheckBox.IsChecked = spell.CurrentLevel < spell.MaximumLevel;

            if (spell.TargetMode == SpellTargetMode.None)
            {
                targetModeComboBox.SelectedValue = "None";
                targetModeComboBox.IsEnabled = false;
            }
            else
                targetModeComboBox.SelectedValue = "Self";

            if (!SpellMetadataManager.Instance.ContainsSpell(spell.Name))
            {
                WarningBorder.Visibility = Visibility.Visible;

                var opacityAnimation = new DoubleAnimation(1.0, 0.25, new Duration(TimeSpan.FromSeconds(0.4)));
                opacityAnimation.AccelerationRatio = 0.75;
                opacityAnimation.AutoReverse = true;
                opacityAnimation.RepeatBehavior = RepeatBehavior.Forever;

                WarningIcon.BeginAnimation(FrameworkElement.OpacityProperty, opacityAnimation);
            }
        }

        public SpellTargetWindow()
        {
            InitializeComponent();
            InitializeViews();

            ToggleTargetMode(TargetCoordinateUnits.None);
            WarningBorder.Visibility = Visibility.Collapsed;
        }

        private void InitializeViews()
        {
            PlayerManager.Instance.PlayerAdded += OnPlayerCollectionChanged;
            PlayerManager.Instance.PlayerUpdated += OnPlayerCollectionChanged;
            PlayerManager.Instance.PlayerRemoved += OnPlayerCollectionChanged;

            PlayerManager.Instance.PlayerPropertyChanged += OnPlayerPropertyChanged;
        }

        private void OnPlayerCollectionChanged(object sender, PlayerEventArgs e)
        {
            Dispatcher.InvokeIfRequired(() =>
            {
                BindingOperations.GetBindingExpression(characterComboBox, ListView.ItemsSourceProperty).UpdateTarget();

            }, DispatcherPriority.DataBind);
        }

        private void OnPlayerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!(sender is Player player))
                return;

            if (string.Equals("Name", e.PropertyName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals("IsLoggedIn", e.PropertyName, StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.InvokeIfRequired(() =>
                {
                    BindingOperations.GetBindingExpression(characterComboBox, ListView.ItemsSourceProperty).UpdateTarget();
                    characterComboBox.Items.Refresh();

                }, DispatcherPriority.DataBind);
            }
        }

        private bool ValidateSpellTarget()
        {
            #region Spell Check
            if (Spell == null)
            {
                this.ShowMessageBox("Invalid Spell",
                   "This spell is no longer valid.",
                   "This spell window will now close, please try again.",
                   MessageBoxButton.OK);

                Close();
                return false;
            }
            #endregion

            var selectedMode = GetSelectedMode();

            #region Check Target Mode
            if (Spell.TargetMode == SpellTargetMode.Target && selectedMode == TargetCoordinateUnits.None)
            {
                this.ShowMessageBox("Target Required",
                   "This spell requires a target.",
                   "You must select a target mode from the dropdown list.",
                   MessageBoxButton.OK);

                targetModeComboBox.Focus();
                targetModeComboBox.IsDropDownOpen = true;
                return false;
            }
            #endregion

            var characterName = characterComboBox.SelectedValue as string;

            if (selectedMode == TargetCoordinateUnits.Character && string.IsNullOrWhiteSpace(characterName))
            {
                this.ShowMessageBox("Invalid Character",
                   "Alternate character cannot be empty.",
                   "If the character you are looking for does not show up\nclose this window and try again.",
                   MessageBoxButton.OK,
                   440, 220);

                return false;
            }

            if ((selectedMode == TargetCoordinateUnits.RelativeRadius || selectedMode == TargetCoordinateUnits.AbsoluteRadius) &&
               innerRadiusUpDown.Value > outerRadiusUpDown.Value)
            {
                this.ShowMessageBox("Invalid Radius",
                   "The inner radius must be less than or equal to the outer radius.",
                   "You may use zero inner radius to include yourself, one to start from adjacent tiles",
                   MessageBoxButton.OK,
                   440, 220);

                return false;
            }

            spellQueueItem.Icon = Spell.Icon;
            spellQueueItem.Name = Spell.Name;
            spellQueueItem.CurrentLevel = Spell.CurrentLevel;
            spellQueueItem.MaximumLevel = Spell.MaximumLevel;

            if (!IsEditMode)
                spellQueueItem.StartingLevel = Spell.CurrentLevel;

            spellQueueItem.Target.Units = selectedMode;

            if (selectedMode == TargetCoordinateUnits.Character)
                spellQueueItem.Target.CharacterName = characterName;
            else
                spellQueueItem.Target.CharacterName = null;

            spellQueueItem.Target.Location = GetLocationForMode(selectedMode);
            spellQueueItem.Target.Offset = new Point(offsetXUpDown.Value, offsetYUpDown.Value);

            if (selectedMode == TargetCoordinateUnits.AbsoluteRadius || selectedMode == TargetCoordinateUnits.RelativeRadius)
            {
                spellQueueItem.Target.InnerRadius = (int)innerRadiusUpDown.Value;
                spellQueueItem.Target.OuterRadius = (int)outerRadiusUpDown.Value;
            }
            else
            {
                spellQueueItem.Target.InnerRadius = 0;
                spellQueueItem.Target.OuterRadius = 0;
            }

            if (!maxLevelCheckBox.IsChecked.Value)
                spellQueueItem.TargetLevel = null;
            else
                spellQueueItem.TargetLevel = (int)maxLevelUpDown.Value;

            return true;
        }

        private TargetCoordinateUnits GetSelectedMode()
        {
            TargetCoordinateUnits mode = TargetCoordinateUnits.None;

            if (targetModeComboBox == null)
                return mode;

            if (!(targetModeComboBox.SelectedValue is string setting))
                return mode;

            Enum.TryParse(setting, out mode);
            return mode;
        }

        private Point GetLocationForMode(TargetCoordinateUnits units)
        {
            switch (units)
            {
                case TargetCoordinateUnits.AbsoluteTile:
                    return new Point(absoluteTileXUpDown.Value, absoluteTileYUpDown.Value);

                case TargetCoordinateUnits.AbsoluteXY:
                    return new Point(absoluteXUpDown.Value, absoluteYUpDown.Value);

                case TargetCoordinateUnits.RelativeTile:
                    return new Point((int)relativeTileXComboBox.SelectedValue, (int)relativeTileYComboBox.SelectedValue);

                case TargetCoordinateUnits.RelativeRadius:
                    goto case TargetCoordinateUnits.RelativeTile;

                case TargetCoordinateUnits.AbsoluteRadius:
                    goto case TargetCoordinateUnits.AbsoluteTile;

                default:
                    return new Point(0, 0);
            }
        }

        private void SetTargetForMode(SpellTarget target)
        {
            if (target == null)
                return;

            targetModeComboBox.SelectedValue = target.Units.ToString();

            switch (target.Units)
            {
                case TargetCoordinateUnits.Character:
                    characterComboBox.SelectedValue = target.CharacterName;
                    break;

                case TargetCoordinateUnits.AbsoluteTile:
                    absoluteTileXUpDown.Value = target.Location.X;
                    absoluteTileYUpDown.Value = target.Location.Y;
                    break;

                case TargetCoordinateUnits.AbsoluteXY:
                    absoluteXUpDown.Value = target.Location.X;
                    absoluteYUpDown.Value = target.Location.Y;
                    break;

                case TargetCoordinateUnits.RelativeTile:
                    relativeTileXComboBox.SelectedItem = (int)target.Location.X;
                    relativeTileYComboBox.SelectedItem = (int)target.Location.Y;
                    break;

                case TargetCoordinateUnits.RelativeRadius:
                    innerRadiusUpDown.Value = target.InnerRadius;
                    outerRadiusUpDown.Value = target.OuterRadius;
                    goto case TargetCoordinateUnits.RelativeTile;

                case TargetCoordinateUnits.AbsoluteRadius:
                    innerRadiusUpDown.Value = target.InnerRadius;
                    outerRadiusUpDown.Value = target.OuterRadius;
                    goto case TargetCoordinateUnits.AbsoluteTile;
            }

            offsetXUpDown.Value = target.Offset.X;
            offsetYUpDown.Value = target.Offset.Y;
        }

        private void ToggleTargetMode(TargetCoordinateUnits units)
        {
            var requiresTarget = units != TargetCoordinateUnits.None;
            var isSelfTarget = units == TargetCoordinateUnits.Self;
            var isRadius = units == TargetCoordinateUnits.AbsoluteRadius || units == TargetCoordinateUnits.RelativeRadius;

            if (characterComboBox != null)
                characterComboBox.Visibility = (units == TargetCoordinateUnits.Character) ? Visibility.Visible : Visibility.Collapsed;

            if (relativeTileXComboBox != null)
                relativeTileXComboBox.Visibility = (units == TargetCoordinateUnits.RelativeTile || units == TargetCoordinateUnits.RelativeRadius) ? Visibility.Visible : Visibility.Collapsed;

            if (absoluteTileXUpDown != null)
                absoluteTileXUpDown.Visibility = (units == TargetCoordinateUnits.AbsoluteTile || units == TargetCoordinateUnits.AbsoluteRadius) ? Visibility.Visible : Visibility.Collapsed;

            if (absoluteXUpDown != null)
                absoluteXUpDown.Visibility = (units == TargetCoordinateUnits.AbsoluteXY) ? Visibility.Visible : Visibility.Collapsed;

            if (offsetXUpDown != null)
                offsetXUpDown.Visibility = (units != TargetCoordinateUnits.None) ? Visibility.Visible : Visibility.Collapsed;

            if (innerRadiusUpDown != null)
                innerRadiusUpDown.Visibility = isRadius ? Visibility.Visible : Visibility.Collapsed;

            if (outerRadiusUpDown != null)
                outerRadiusUpDown.Visibility = isRadius ? Visibility.Visible : Visibility.Collapsed;

            // Store this the first time before resizing
            if (baseHeight <= 0)
                baseHeight = Height;

            var height = baseHeight;

            if (requiresTarget)
                height += 40;

            if (isRadius)
                height += 90;

            if (!isSelfTarget && requiresTarget)
                height += 40;

            Height = height;
        }

        private void targetModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count < 1)
            {
                ToggleTargetMode(TargetCoordinateUnits.None);
                return;
            }

            if (!(e.AddedItems[0] is UserSetting item))
            {
                ToggleTargetMode(TargetCoordinateUnits.None);
                return;
            }

            if (!Enum.TryParse<TargetCoordinateUnits>(item.Value as string, out var mode))
                mode = TargetCoordinateUnits.None;

            ToggleTargetMode(mode);
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateSpellTarget())
                return;

            DialogResult = true;
            Close();
        }
    }
}
