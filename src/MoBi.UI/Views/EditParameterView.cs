﻿using OSPSuite.DataBinding;
using OSPSuite.DataBinding.DevExpress;
using OSPSuite.DataBinding.DevExpress.XtraGrid;
using OSPSuite.UI.Extensions;
using OSPSuite.Utility.Extensions;
using DevExpress.Utils;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraLayout.Utils;
using MoBi.Assets;
using MoBi.Presentation.DTO;
using MoBi.Presentation.Presenter;
using MoBi.Presentation.Views;
using OSPSuite.Core.Domain;
using OSPSuite.Presentation.Extensions;
using OSPSuite.Presentation.Views;
using OSPSuite.Assets;
using OSPSuite.UI.Binders;
using OSPSuite.UI.Controls;
using ToolTips = MoBi.Assets.ToolTips;

namespace MoBi.UI.Views
{
   public partial class EditParameterView : BaseUserControl, IEditParameterView
   {
      private IEditParameterPresenter _presenter;
      private readonly ScreenBinder<ParameterDTO> _screenBinder;
      private readonly GridViewBinder<TagDTO> _gridBinder;

      public EditParameterView()
      {
         InitializeComponent();
         _screenBinder = new ScreenBinder<ParameterDTO>();
         _gridBinder = new GridViewBinder<TagDTO>(gridViewTags);
      }

      public override void InitializeBinding()
      {
         base.InitializeBinding();
         _screenBinder.Bind(dto => dto.Name)
            .To(btName)
            .OnValueUpdating += onNameSet;

         _screenBinder.Bind(dto => dto.Group)
            .To(cbGroup)
            .WithValues(dto => _presenter.AllGroups())
            .AndDisplays(g => _presenter.DisplayFor(g))
            .OnValueUpdating += (o, e) => OnEvent(() => _presenter.SetGroup(e.NewValue));

         _screenBinder.Bind(dto => dto.Dimension)
            .To(cbDimension)
            .WithValues(dto => _presenter.AllDimensions())
            .OnValueUpdating += (o, e) => OnEvent(() => _presenter.SetDimension(e.NewValue));

         _screenBinder.Bind(dto => dto.HasRHS)
            .To(chkHasRHS)
            .OnValueUpdating += (o, e) => OnEvent(() => onRHSValueValueSet(o, e.NewValue));

         _screenBinder.Bind(dto => dto.Persistable)
            .To(chkPersistable)
            .OnValueUpdating += (o, e) => OnEvent(() => _presenter.SetPersistable(e.NewValue));

         _screenBinder.Bind(dto => dto.BuildMode)
            .To(cbParameterBuildMode)
            .WithValues(x => _presenter.ParameterBuildModes)
            .OnValueUpdating += onBuildModeSet;

         _screenBinder.Bind(dto => dto.Description)
            .To(htmlEditor)
            .OnValueUpdating += onDescriptionSet;

         _screenBinder.Bind(dto => dto.IsAdvancedParameter)
            .To(chkAdvancedParameter)
            .OnValueUpdating += (o, e) => OnEvent(() => _presenter.SetIsAdvancedParameter(e.NewValue));

         _screenBinder.Bind(dto => dto.CanBeVariedInPopulation)
            .To(chkCanBeVariedInPopulation)
            .OnValueUpdating += (o, e) => OnEvent(() => _presenter.SetIsVariablePopulation(e.NewValue));

         _screenBinder.Bind(dto => dto.IsFavorite)
            .To(chkIsFavorite)
            .OnValueUpdating += (o, e) => OnEvent(() => _presenter.SetIsFavorite(e.NewValue));

         RegisterValidationFor(_screenBinder, NotifyViewChanged);

         _gridBinder.Bind(tag => tag.Value).WithCaption(AppConstants.Captions.Tag).AsReadOnly();
         var buttonRepository = createAddRemoveButtonRepository();
         buttonRepository.ButtonClick += (o, e) => OnEvent(() => onButtonClicked(e, _gridBinder.FocusedElement));

         _gridBinder.AddUnboundColumn()
            .WithCaption(OSPSuite.UI.UIConstants.EMPTY_COLUMN)
            .WithShowButton(ShowButtonModeEnum.ShowAlways)
            .WithRepository(dto => buttonRepository)
            .WithFixedWidth(OSPSuite.UI.UIConstants.Size.EMBEDDED_BUTTON_WIDTH * 2);

         btAddTag.Click += (o, e) => OnEvent(_presenter.AddNewTag);
         btName.ButtonClick += (o, e) => OnEvent(nameButtonClicked, e);
      }

      private void nameButtonClicked(ButtonPressedEventArgs e)
      {
         _presenter.RenameParameter();
      }

      private void onButtonClicked(ButtonPressedEventArgs buttonPressedEventArgs, TagDTO tagDTO)
      {
         var pressedButton = buttonPressedEventArgs.Button;
         if (pressedButton.Kind.Equals(ButtonPredefines.Plus))
            _presenter.AddNewTag();
         else
            _presenter.RemoveTag(tagDTO);
      }

      private RepositoryItemButtonEdit createAddRemoveButtonRepository()
      {
         var buttonRepository = new RepositoryItemButtonEdit {TextEditStyle = TextEditStyles.HideTextEditor};
         buttonRepository.Buttons[0].Kind = ButtonPredefines.Plus;
         buttonRepository.Buttons.Add(new EditorButton(ButtonPredefines.Delete));
         return buttonRepository;
      }

      public override void InitializeResources()
      {
         base.InitializeResources();
         btName.ToolTip = ToolTips.ParameterView.ParameterName;
         cbParameterBuildMode.ToolTip = ToolTips.ParameterView.ParameterType;
         cbDimension.ToolTip = ToolTips.ParameterView.ParameterDimension;
         htmlEditor.ToolTip = ToolTips.Description;
         chkHasRHS.ToolTip = ToolTips.ParameterView.IsStateVariable;
         chkAdvancedParameter.Text = AppConstants.Captions.IsAdvancedParameter;
         chkPersistable.Text = AppConstants.Captions.Persistable;
         chkPersistable.ToolTip = ToolTips.ParameterView.Persistable;
         chkCanBeVariedInPopulation.Text = AppConstants.Captions.CanBeVariedInPopulation;
         tabProperties.Text = AppConstants.Captions.Properties;
         tabProperties.Image = ApplicationIcons.Properties;
         tabTags.Text = AppConstants.Captions.Tags;
         tabTags.Image = ApplicationIcons.Tag;
         btAddTag.InitWithImage(ApplicationIcons.Add, AppConstants.Captions.AddTag);
         layoutControlItemAddTag.AdjustButtonSize();
         layoutControlItemTags.TextLocation = Locations.Top;
         layoutControlItemTags.Text = AppConstants.Captions.Tags;
         layoutItemGroup.Text = AppConstants.Captions.Group.FormatForLabel();
         layoutItemValueOrigin.Text = Captions.ValueOrigin.FormatForLabel();
         chkIsFavorite.Text = Captions.Favorite;
         layoutGroupProperties.Text = AppConstants.Captions.Properties;
         layoutItemValueOrigin.AdjustControlHeight(layoutItemGroup.Control.Height);
      }

      public void Activate()
      {
         ActiveControl = btName;
      }

      private void onRHSValueValueSet(ParameterDTO parameter, bool useRHS)
      {
         parameter.HasRHS = useRHS;
         showRHSPanel = parameter.HasRHS;
         _presenter.SetUseRHSFormula(useRHS);
         //we need to trigger the view changed event to ensure that the CanClose is evaluated again
         NotifyViewChanged();
      }

      private void onDescriptionSet(ParameterDTO parameterDTO, PropertyValueSetEventArgs<string> propertySetEventArgs)
      {
         OnEvent(() => _presenter.SetDescription(parameterDTO, propertySetEventArgs.NewValue));
      }

      private void onBuildModeSet(ParameterDTO parameterDTO, PropertyValueSetEventArgs<ParameterBuildMode> propertySetEventArgs)
      {
         OnEvent(() => _presenter.SetBuildMode(parameterDTO, propertySetEventArgs.NewValue));
      }

      private void onNameSet(ParameterDTO parameterDTO, PropertyValueSetEventArgs<string> propertySetEventArgs)
      {
         OnEvent(() => _presenter.SetName(parameterDTO, propertySetEventArgs.NewValue));
      }

      public void AttachPresenter(IEditParameterPresenter presenter)
      {
         _presenter = presenter;
      }

      public void Show(ParameterDTO parameterDTO)
      {
         _screenBinder.BindToSource(parameterDTO);
         _gridBinder.BindToSource(parameterDTO.Tags);
         initNameControl(parameterDTO);
         initRHSControl(parameterDTO);
      }

      private void initRHSControl(ParameterDTO parameterDTO)
      {
         showRHSPanel = !parameterDTO.RHSFormula.Equals(FormulaBuilderDTO.NULL);
      }

      private void initNameControl(ParameterDTO parameterDTO)
      {
         var isNewParameter = parameterDTO.Name.IsNullOrEmpty();
         btName.Properties.ReadOnly = !isNewParameter;
         btName.Properties.Buttons[0].Visible = !isNewParameter;
      }

      public void SetFormulaView(IView valueView)
      {
         panelFormula.FillWith(valueView);
      }

      public void AddRHSView(IView rhsView)
      {
         panelRHSFormula.FillWith(rhsView);
      }

      public bool ShowBuildMode
      {
         set => layoutItemParameterType.Visibility = LayoutVisibilityConvertor.FromBoolean(value);
      }

      public void AddValueOriginView(IView view)
      {
         panelOrigiView.FillWith(view);
      }

      private bool showRHSPanel
      {
         set
         {
            layoutGroupRHSFormula.Visibility = LayoutVisibilityConvertor.FromBoolean(value);
            splitterRHSFormula.Visibility = layoutGroupRHSFormula.Visibility;
            layoutGroupValue.Text = value ? AppConstants.Captions.InitialValue : AppConstants.Captions.Value;
         }
      }

      public override bool HasError => base.HasError || _screenBinder.HasError;
   }
}