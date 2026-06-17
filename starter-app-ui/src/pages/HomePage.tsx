import React, { useState } from 'react';
import { Card, CardContent, CardHeader, Button, Spinner, Chip } from '@heroui/react';
import { FormField } from '../components/FormField';
import { useAuth } from '../hooks/useAuth';
import { useHelloV1, useHelloV2, useInfiniteNotes, useCreateNote, useDeleteNote } from '../hooks/api';
import type { Note } from '../types/models';

export function HomePage() {
  const { user } = useAuth();
  const { data: helloV1, isLoading: loadingV1 } = useHelloV1();
  const { data: helloV2, isLoading: loadingV2 } = useHelloV2();

  const {
    data: notesData,
    isLoading: loadingNotes,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage
  } = useInfiniteNotes(5);
  const createNote = useCreateNote();
  const deleteNote = useDeleteNote();

  const allNotes: Note[] = (notesData?.pages ?? []).flatMap(page => page.nodes ?? page.edges?.map(e => e.node) ?? []);

  const [noteTitle, setNoteTitle] = useState('');
  const [noteContent, setNoteContent] = useState('');

  const handleCreateNote = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!noteTitle.trim()) return;

    await createNote.mutateAsync({ title: noteTitle.trim(), content: noteContent.trim() });
    setNoteTitle('');
    setNoteContent('');
  };

  return (
    <div className="space-y-8">
      {/* Welcome banner */}
      <Card>
        <CardHeader>
          <h1 className="text-3xl font-bold text-primary">Welcome, {user?.userName}!</h1>
        </CardHeader>
        <CardContent>
          <p className="text-default-600">You are authenticated. Here's a live hello-world check against the API.</p>
        </CardContent>
      </Card>

      {/* API Hello checks */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Card>
          <CardHeader>
            <h2 className="text-lg font-semibold">GET /api/v1/hello</h2>
          </CardHeader>
          <CardContent>
            {loadingV1 ? (
              <Spinner size="sm" color="accent" />
            ) : helloV1 ? (
              <div className="space-y-1">
                <Chip color="success" variant="soft">OK</Chip>
                <p className="text-sm text-default-600 mt-2">{helloV1.message}</p>
                {helloV1.version && <p className="text-xs text-default-400">v{helloV1.version}</p>}
              </div>
            ) : (
              <Chip color="warning" variant="soft">Not available</Chip>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <h2 className="text-lg font-semibold">GET /api/v2/hello</h2>
          </CardHeader>
          <CardContent>
            {loadingV2 ? (
              <Spinner size="sm" color="accent" />
            ) : helloV2 ? (
              <div className="space-y-1">
                <Chip color="success" variant="soft">OK</Chip>
                <p className="text-sm text-default-600 mt-2">{helloV2.message}</p>
                {helloV2.version && <p className="text-xs text-default-400">v{helloV2.version}</p>}
              </div>
            ) : (
              <Chip color="warning" variant="soft">Not available</Chip>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Notes section */}
      <Card>
        <CardHeader>
          <h2 className="text-xl font-semibold">Your Notes</h2>
        </CardHeader>
        <CardContent className="space-y-6">
          {/* Create note form */}
          <form onSubmit={handleCreateNote} className="space-y-3 border border-default-200 rounded-lg p-4">
            <h3 className="text-sm font-semibold text-default-700">Create Note</h3>
            <FormField
              label="Title"
              value={noteTitle}
              onChange={setNoteTitle}
              isRequired
            />
            <FormField
              label="Content"
              value={noteContent}
              onChange={setNoteContent}
            />
            <Button
              type="submit"
              isPending={createNote.isPending}
              isDisabled={!noteTitle.trim()}
            >
              {({ isPending }) => (
                <>
                  {isPending && <Spinner color="current" size="sm" className="mr-2" />}
                  {isPending ? 'Adding...' : 'Add Note'}
                </>
              )}
            </Button>
          </form>

          {/* Notes list */}
          {loadingNotes ? (
            <div className="flex justify-center py-8">
              <Spinner size="lg" color="accent" />
            </div>
          ) : allNotes.length === 0 ? (
            <p className="text-default-500 text-center py-4">No notes yet. Create your first one above!</p>
          ) : (
            <div className="space-y-3">
              {allNotes.map(note => (
                <div key={note.id} className="flex items-start justify-between p-3 bg-content2 rounded-lg">
                  <div className="flex-1 min-w-0">
                    <p className="font-medium text-foreground">{note.title}</p>
                    {note.content && <p className="text-sm text-default-500 truncate">{note.content}</p>}
                    <p className="text-xs text-default-400 mt-1">{new Date(note.createdAt).toLocaleDateString()}</p>
                  </div>
                  <Button
                    variant="danger-soft"
                    isPending={deleteNote.isPending}
                    onPress={() => deleteNote.mutate(note.id)}
                    className="ml-2 shrink-0"
                  >
                    {({ isPending }) => isPending ? <Spinner color="current" size="sm" /> : 'Delete'}
                  </Button>
                </div>
              ))}

              {hasNextPage && (
                <Button
                  variant="outline"
                  fullWidth
                  isDisabled={isFetchingNextPage}
                  onPress={() => fetchNextPage()}
                >
                  {isFetchingNextPage ? <Spinner color="current" size="sm" className="mr-2" /> : null}
                  {isFetchingNextPage ? 'Loading...' : 'Load More'}
                </Button>
              )}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
