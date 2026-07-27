type Review = {
  id: string;
  rating: number;
  comment?: string | null;
  createdAt: string;
};

export function ReviewList({ reviews }: { reviews?: Review[] }) {
  if (!reviews?.length) {
    return <p className="muted">Отзывов пока нет. Первые сделки — первые звёзды.</p>;
  }

  return (
    <div className="stack">
      {reviews.map((review) => (
        <article className="card" key={review.id}>
          <strong>{"⭐".repeat(review.rating)}</strong>
          <p>{review.comment}</p>
          <small className="muted">{new Date(review.createdAt).toLocaleDateString()}</small>
        </article>
      ))}
    </div>
  );
}
