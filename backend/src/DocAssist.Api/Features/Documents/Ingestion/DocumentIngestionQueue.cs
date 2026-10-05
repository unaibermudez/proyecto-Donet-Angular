using System.Threading.Channels;

namespace DocAssist.Api.Features.Documents.Ingestion;

// Cola en memoria de documentos por procesar. Los endpoints escriben en ella y
// DocumentIngestionWorker lee de ella. Un Channel es una cola segura entre hilos en la
// que el lector espera sin consumir CPU hasta que llega algo.
//
// Al estar en memoria, si la aplicación se para se pierde lo que hubiera en la cola.
// No pasa nada: el estado está en la base de datos (Pending/Processing) y el worker
// vuelve a encolar esos documentos al arrancar.
public sealed class DocumentIngestionQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public void Enqueue(int documentId) => _channel.Writer.TryWrite(documentId);

    public IAsyncEnumerable<int> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
