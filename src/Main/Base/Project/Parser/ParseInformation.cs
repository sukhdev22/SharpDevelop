﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.Core;
using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.Editor;
using ICSharpCode.NRefactory.TypeSystem;

namespace ICSharpCode.SharpDevelop.Parser
{
	/// <summary>
	/// Represents the result of a parser operation.
	/// Note: the parser service only stores the IUnresolvedFile, not any of the extra information.
	/// The extra information is only provided to listeners of the ParseInformationUpdated event.
	/// Those listeners may then decide to store the extra information (e.g. the TaskService stores TagComments).
	/// </summary>
	public class ParseInformation
	{
		readonly IUnresolvedFile unresolvedFile;
		IList<TagComment> tagComments = new List<TagComment>();
		readonly bool isFullParseInformation;
		
		public ParseInformation(IUnresolvedFile unresolvedFile, bool isFullParseInformation)
		{
			if (unresolvedFile == null)
				throw new ArgumentNullException("unresolvedFile");
			this.unresolvedFile = unresolvedFile;
			this.isFullParseInformation = isFullParseInformation;
		}
		
		/// <summary>
		/// Gets whether this parse information contains 'extra' data.
		/// True = extra data is provided (e.g. folding information).
		/// False = Only UnresolvedFile and TagComments are provided.
		/// </summary>
		public bool IsFullParseInformation {
			get { return isFullParseInformation; }
		}
		
		public IUnresolvedFile UnresolvedFile {
			get { return unresolvedFile; }
		}
		
		public FileName FileName {
			get { return FileName.Create(unresolvedFile.FileName); }
		}
		
		public IList<TagComment> TagComments {
			get { return tagComments; }
		}
		
		#region Folding
		/// <summary>
		/// Gets whether this <see cref="ParseInformation"/> instance supports folding.
		/// The default is true.
		/// </summary>
		public virtual bool SupportsFolding {
			get { return true; }
		}
		
		/// <summary>
		/// Creates fold markers from this parse information.
		/// The base implementation of this method uses the unresolved file to produce
		/// fold markers for all type defintions and methods.
		/// </summary>
		/// <param name="document"></param>
		/// <param name="firstErrorOffset"></param>
		/// <returns></returns>
		public virtual IEnumerable<NewFolding> GetFoldings(IDocument document, out int firstErrorOffset)
		{
			firstErrorOffset = -1;
			List<NewFolding> newFoldMarkers = new List<NewFolding>();
			foreach (var c in this.UnresolvedFile.TopLevelTypeDefinitions) {
				AddClassMembers(document, c, newFoldMarkers);
			}
			return newFoldMarkers;
		}
		
		static void AddClassMembers(IDocument document, IUnresolvedTypeDefinition c, List<NewFolding> newFoldMarkers)
		{
			if (c.Kind == TypeKind.Delegate) {
				return;
			}
			DomRegion cRegion = c.BodyRegion;
			if (c.BodyRegion.BeginLine < c.BodyRegion.EndLine) {
				newFoldMarkers.Add(new NewFolding(GetStartOffset(document, c.BodyRegion), GetEndOffset(document, c.BodyRegion)));
			}
			foreach (var innerClass in c.NestedTypes) {
				AddClassMembers(document, innerClass, newFoldMarkers);
			}
			
			foreach (var m in c.Members) {
				if (m.BodyRegion.BeginLine < m.BodyRegion.EndLine) {
					newFoldMarkers.Add(new NewFolding(GetStartOffset(document, m.BodyRegion), GetEndOffset(document, m.BodyRegion))
					                   { IsDefinition = true });
				}
			}
		}
		
		static int GetStartOffset(IDocument document, DomRegion bodyRegion)
		{
			if (bodyRegion.BeginLine < 1)
				return 0;
			if (bodyRegion.BeginLine > document.LineCount)
				return document.TextLength;
			var line = document.GetLineByNumber(bodyRegion.BeginLine);
			int lineStart = line.Offset;
			int bodyStartOffset = lineStart + bodyRegion.BeginColumn - 1;
			for (int i = lineStart; i < bodyStartOffset; i++) {
				if (!char.IsWhiteSpace(document.GetCharAt(i))) {
					// Non-whitespace in front of body start:
					// Use the body start as start offset
					return bodyStartOffset;
				}
			}
			// Only whitespace in front of body start:
			// Use the end of the previous line as start offset
			return line.PreviousLine != null ? line.PreviousLine.EndOffset : bodyStartOffset;
		}
		
		static int GetEndOffset(IDocument document, DomRegion region)
		{
			if (region.EndLine < 1)
				return 0;
			if (region.EndLine > document.LineCount)
				return document.TextLength;
			return document.GetOffset(region.End);
		}
		#endregion
	}
}