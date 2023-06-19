using System.Linq;
using EnvDTE;
using Project = EnvDTE.Project;

namespace BuildOnSave
{
	static class DTEExtensions
	{
		public static Document[] UnsavedDocumentsBelongingToAProject(this DTE dte)
		{
			// note: this might have the side effect of opening a project's property page
			// and throwing a COMException.
			var allDocuments = dte.Documents.Cast<Document>().ToArray();
			var unsavedDocuments = allDocuments.Where(document => !document.Saved).ToArray();
			var unsavedBelongingToAnOpenProject = unsavedDocuments
				.Where(document => document.BelongsToAnOpenProject())
				.ToArray();

			return unsavedBelongingToAnOpenProject;
		}

		public static Project[] UnsavedOpenProjects(this DTE dte)
		{
			return dte.Solution.Projects
				.Cast<Project>()
				.Where(p => !p.Saved)
				.ToArray();
		}
		public static bool BelongsToAnOpenProject(this Document document)
		{
			return document.ProjectItem.ContainingProject.FullName != "";
		}

		public static bool IsLoaded(this Project project)
		{
			return project.Kind != Constants.vsProjectKindUnmodeled;
		}

		public static Project[] GetAllProjects(this EnvDTE80.Solution2 sln)
		{
			return sln.Projects
				.Cast<Project>()
				.SelectMany(GetProjects)
				.ToArray();
		}

		static Project[] GetProjects(Project project)
		{
			switch (project.Kind)
			{
				case Constants.vsProjectKindMisc:
					return new Project[]{ };

				case Constants.vsProjectKindSolutionItems:
					return project.ProjectItems
						.Cast<ProjectItem>()
						.Select(x => x.SubProject)
						.Where(x => x != null)
						.SelectMany(GetProjects)
						.ToArray();

				default:
					return new[] { project };
			}
		}
	}
}
