﻿using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using MoBi.Assets;
using MoBi.Core.Domain.Extensions;
using MoBi.Core.Domain.Model;
using MoBi.Core.Domain.Model.Diagram;
using MoBi.Presentation.Nodes;
using MoBi.Presentation.Settings;
using MoBi.Presentation.Tasks;
using MoBi.Presentation.Views;
using OSPSuite.Core.Chart;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Data;
using OSPSuite.Core.Events;
using OSPSuite.Presentation.Binders;
using OSPSuite.Presentation.Core;
using OSPSuite.Presentation.DTO;
using OSPSuite.Presentation.Extensions;
using OSPSuite.Presentation.MenuAndBars;
using OSPSuite.Presentation.Nodes;
using OSPSuite.Presentation.Presenters;
using OSPSuite.Presentation.Presenters.Charts;
using OSPSuite.Presentation.Services.Charts;
using OSPSuite.Utility.Collections;
using OSPSuite.Utility.Events;
using OSPSuite.Utility.Extensions;
using IChartTemplatingTask = MoBi.Presentation.Tasks.IChartTemplatingTask;

namespace MoBi.Presentation.Presenter
{
   /// <summary>
   ///    Aggregates the presenters needed to show a chart
   /// </summary>
   /// <remarks>
   ///    Using facade pattern
   /// </remarks>
   public interface IChartPresenter : IPresenter<IChartView>,
      IListener<ObservedDataRemovedEvent>,
      IListener<ChartTemplatesChangedEvent>
   {
      CurveChart Chart { get; }

      /// <summary>
      ///    Shows the specified chart with underlying data.
      /// </summary>
      /// <param name="chart">The chart to display</param>
      /// <param name="dataRepositories">The data used in the chart</param>
      /// <param name="defaultTemplate">If specified, this template will be used to initialize the chart</param>
      /// <remarks>
      ///    This method ensures the correct order of parameter setting
      /// </remarks>
      void Show(CurveChart chart, IReadOnlyList<DataRepository> dataRepositories, CurveChartTemplate defaultTemplate = null);

      void UpdateTemplatesFor(IWithChartTemplates withChartTemplates);
   }

   public abstract class ChartPresenter : ChartPresenter<CurveChart, IChartView, IChartPresenter>, IChartPresenter
   {
      protected readonly IMoBiContext _context;
      private readonly IUserSettings _userSettings;
      private readonly IChartTasks _chartTasks;
      protected readonly IChartTemplatingTask _chartTemplatingTask;
      protected readonly ICache<DataRepository, IMoBiSimulation> _dataRepositoryCache;

      private readonly ObservedDataDragDropBinder _observedDataDragDropBinder;

      private IChartDisplayPresenter displayPresenter => _chartPresenterContext.DisplayPresenter;
      private IChartEditorPresenter editorPresenter => _chartPresenterContext.EditorPresenter;

      protected ChartPresenter(IChartView chartView, ChartPresenterContext chartPresenterContext, IMoBiContext context, IUserSettings userSettings, IChartTasks chartTasks,
         IChartTemplatingTask chartTemplatingTask) :
         base(chartView, chartPresenterContext)
      {
         _chartTasks = chartTasks;
         initializeDisplayPresenter();
         initializeEditorPresenter();

         _chartTemplatingTask = chartTemplatingTask;
         _dataRepositoryCache = new Cache<DataRepository, IMoBiSimulation>(onMissingKey: x => null);

         _userSettings = userSettings;
         _context = context;

         _view.SetChartView(chartPresenterContext.EditorAndDisplayPresenter.BaseView);

         initLayout();
         initEditorPresenterSettings();

         _observedDataDragDropBinder = new ObservedDataDragDropBinder();

         AddSubPresenters(chartPresenterContext.EditorAndDisplayPresenter);
      }

      private void initializeEditorPresenter()
      {
         editorPresenter.SetCurveNameDefinition(CurveNameDefinition);
      }

      private void initializeDisplayPresenter()
      {
         displayPresenter.DragDrop += OnDragDrop;
         displayPresenter.DragOver += OnDragOver;
         displayPresenter.ExportToPDF = () => _chartTasks.ExportToPDF(Chart);
         initializeNoCurvesHint();
      }

      private void clearNoCurvesHint()
      {
         displayPresenter.SetNoCurvesSelectedHint(string.Empty);
      }

      private void initializeNoCurvesHint()
      {
         displayPresenter.SetNoCurvesSelectedHint(AppConstants.PleaseSelectCurveInChartEditor);
      }

      protected ChartOptions ChartOptions => _userSettings.ChartOptions;

      protected abstract string CurveNameDefinition(DataColumn column);

      private void initEditorPresenterSettings()
      {
         editorPresenter.SetDisplayQuantityPathDefinition(displayPathForColumn);
         //Show all Columns
         editorPresenter.SetShowDataColumnInDataBrowserDefinition(col => true);
      }

      private PathElements displayPathForColumn(DataColumn column)
      {
         var simulationForDataColumn = _dataRepositoryCache[column.Repository];
         var rootContainerForDataColumn = simulationForDataColumn?.Model.Root;
         return _chartPresenterContext.DataColumnToPathElementsMapper.MapFrom(column, rootContainerForDataColumn);
      }

      protected override ISimulation SimulationFor(DataColumn dataColumn)
      {
         return findSimulation(dataColumn.Repository);
      }

      protected void AddMenuButtons()
      {
         AllMenuButtons().Each(editorPresenter.AddButton);
         editorPresenter.AddUsedInMenuItem();
      }

      protected void ClearMenuButtons()
      {
         editorPresenter.ClearButtons();
      }

      protected virtual IEnumerable<IMenuBarItem> AllMenuButtons()
      {
         yield return _chartPresenterContext.EditorAndDisplayPresenter.ChartLayoutButton;
      }

      private void initLayout()
      {
         _chartPresenterContext.EditorLayoutTask.InitFromUserSettings(_chartPresenterContext.EditorAndDisplayPresenter);
      }

      protected void LoadFromTemplate(CurveChartTemplate chartTemplate, bool triggeredManually, bool propagateChartChangeEvent = true)
      {
         _chartTemplatingTask.InitFromTemplate(_dataRepositoryCache, Chart, editorPresenter, chartTemplate, CurveNameDefinition, triggeredManually, propagateChartChangeEvent);
      }

      protected virtual void OnDragOver(object sender, IDragEvent e)
      {
         if (simulationResultsIsBeingDragged(e))
            e.Effect = CanDropSimulation ? DragEffect.Move : DragEffect.None;
         else
            _observedDataDragDropBinder.PrepareDrag(e);
      }

      private static bool simulationResultsIsBeingDragged(IDragEvent e)
      {
         var data = e.Data<IList<ITreeNode>>();
         //do not use null propagation as suggested by resharper
         // ReSharper disable once UseNullPropagation
         if (data == null)
            return false;

         return data.Count == data.OfType<HistoricalResultsNode>().Count();
      }

      protected virtual void OnDragDrop(object sender, IDragEvent e)
      {
         if (simulationResultsIsBeingDragged(e) && CanDropSimulation)
         {
            var historicalResultsNodes = e.Data<IList<ITreeNode>>().OfType<HistoricalResultsNode>();
            addHistoricalResults(historicalResultsNodes.Select(result => result.Tag).ToList());
         }
         else
         {
            var droppedObservedData = _observedDataDragDropBinder.DroppedObservedDataFrom(e);
            addObservedData(droppedObservedData.ToList());
         }
      }

      protected abstract bool CanDropSimulation { get; }

      protected abstract void MarkChartOwnerAsChanged();

      private void addHistoricalResults(IReadOnlyList<DataRepository> repositories)
      {
         addDataRepositoriesToDataRepositoryCache(repositories);
         editorPresenter.AddDataRepositories(repositories);
         repositories.Each(repository => repository.SetPersistable(persistable: true));
      }

      private void addObservedData(IReadOnlyList<DataRepository> repositories)
      {
         editorPresenter.AddDataRepositories(repositories);
         repositories.SelectMany(x => x.ObservationColumns()).Each(observationColumn => editorPresenter.AddCurveForColumn(observationColumn));
         _chartPresenterContext.Refresh();
      }

      private void addObservedDataIfNeeded(IEnumerable<DataRepository> dataRepositories)
      {
         //curves are already selected. no need to add default selection
         if (Chart.Curves.Any()) return;
         addObservedData(dataRepositories.ToList());
      }

      protected override void ChartChanged()
      {
         base.ChartChanged();
         MarkChartOwnerAsChanged();
      }

      public void Show(CurveChart chart, IReadOnlyList<DataRepository> dataRepositories, CurveChartTemplate defaultTemplate = null)
      {
         try
         {
            clearNoCurvesHint();
            InitializeAnalysis(chart);
            //do not validate template when showing a chart as the chart might well be without curves when initialized for the first time.
            var currentTemplate = defaultTemplate ?? _chartTemplatingTask.TemplateFrom(chart, validateTemplate: false);
            replaceSimulationRepositories(dataRepositories);
            LoadFromTemplate(currentTemplate, triggeredManually: false, propagateChartChangeEvent: false);
            addObservedDataIfNeeded(dataRepositories);
         }
         finally
         {
            initializeNoCurvesHint();
         }
      }

      public virtual void UpdateTemplatesFor(IWithChartTemplates withChartTemplates)
      {
         UpdateTemplatesBasedOn(withChartTemplates);
      }

      private void addDataRepositoriesToDataRepositoryCache(IReadOnlyCollection<DataRepository> dataRepositories)
      {
         dataRepositories.Where(dataRepository => !_dataRepositoryCache.Contains(dataRepository))
            .Each(dataRepository =>
            {
               var simulation = findSimulation(dataRepository) ?? findHistoricSimulation(dataRepository);
               if (simulation != null)
                  _dataRepositoryCache.Add(dataRepository, simulation);
            });
      }

      private IMoBiSimulation findSimulation(DataRepository dataRepository)
      {
         return _context.CurrentProject.Simulations
            .FirstOrDefault(simulation => Equals(simulation.Results, dataRepository));
      }

      private IMoBiSimulation findHistoricSimulation(DataRepository dataRepository)
      {
         return _context.CurrentProject.Simulations.FirstOrDefault(simulation => simulation.HistoricResults.Contains(dataRepository));
      }

      private void replaceSimulationRepositories(IReadOnlyCollection<DataRepository> dataRepositories)
      {
         var repositoriesToRemove = _dataRepositoryCache.Keys.Except(dataRepositories).ToList();
         repositoriesToRemove.Each(_dataRepositoryCache.Remove);

         addDataRepositoriesToDataRepositoryCache(dataRepositories);

         editorPresenter.AddDataRepositories(dataRepositories);

         var repositories = editorPresenter.AllDataColumns.Select(col => col.Repository).Distinct();
         repositoriesToRemove = repositories.Except(dataRepositories).ToList();
         editorPresenter.RemoveDataRepositories(repositoriesToRemove);
      }

      public void Handle(ObservedDataRemovedEvent eventToHandle)
      {
         var dataRepository = eventToHandle.DataRepository;
         editorPresenter.RemoveDataRepositories(new[] {dataRepository});
         displayPresenter.Refresh();
      }

      public override void ReleaseFrom(IEventPublisher eventPublisher)
      {
         base.ReleaseFrom(eventPublisher);
         displayPresenter.DragDrop -= OnDragDrop;
         displayPresenter.DragOver -= OnDragOver;
         Clear();
      }
   }
}